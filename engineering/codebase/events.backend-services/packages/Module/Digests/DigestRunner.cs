using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Starts digest runs and sends them: a run row unique per digest and occurrence is claimed together with its
/// rendered texts and one pending delivery per recipient, then each delivery goes through the digest's channel under the
/// run's idempotency key for that recipient.</summary>
/// <remarks>
///   - restart-safe: a claimed occurrence never runs twice; a run whose host died is resumed once its lease passes, and a
///     resent message repeats its idempotency key, so the channel sends nothing more
///   - a passing failure (<see cref="DigestDeliveryOutcome.RetryAfter"/>) stays pending until its retry, up to
///     <see cref="EventDigestsOptions.MaxAttempts"/> sends
/// </remarks>
/// <param name="db">The module's store.</param>
/// <param name="drafter">Renders a digest.</param>
/// <param name="deliveries">The registered channels.</param>
/// <param name="time">The clock.</param>
/// <param name="options">The digests' settings.</param>
/// <param name="logger">The log.</param>
internal sealed partial class DigestRunner(
    EventsDbContext db,
    DigestDrafter drafter,
    IEnumerable<IDigestDelivery> deliveries,
    TimeProvider time,
    IOptions<EventDigestsOptions> options,
    ILogger<DigestRunner> logger)
{
    private static readonly string Schedule = EventsLabels.Of(DigestRunTrigger.Schedule);
    private static readonly string Manual = EventsLabels.Of(DigestRunTrigger.Manual);
    private const int MaxErrorLength = 2_000;
    private const int MaxIdempotencyKeyLength = 128;

    /// <summary>Starts the occurrence a due digest owes, or records it missed when it is older than the catch-up window, and
    /// moves the digest's next run past now. An occurrence another host claimed starts nothing.</summary>
    /// <param name="digestId">The digest.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The run started; none when nothing was owed or another host claimed it.</returns>
    public async Task<Guid?> StartDueAsync(Guid digestId, CancellationToken cancellationToken)
    {
        var now = time.StoredUtcNow();
        var digest = await db.Digests.AsNoTracking().FirstOrDefaultAsync(row => row.Id == digestId, cancellationToken);
        if (digest is null || !digest.Enabled || !DigestOccurrences.TryFindZone(digest.TimeZone, out var zone))
            return null;

        var next = DigestRows.NextRunAt(digest, now);
        var owed = DigestOccurrences.Latest(DigestRows.Schedule(digest), zone, digest.ScheduleSince, now) is { } latest ? StoredClock.Truncate(latest) : (DateTimeOffset?)null;
        if (owed is null)
        {
            await MoveNextRunAsync(digest.Id, next, cancellationToken);
            return null;
        }

        var catalog = await db.Catalogs.AsNoTracking().FirstAsync(row => row.Id == digest.CatalogId, cancellationToken);
        var missed = now - owed.Value > options.Value.CatchUpWindow;
        var started = missed
            ? await ClaimMissedAsync(digest, owed.Value, next, now, cancellationToken)
            : await ClaimAsync(digest, catalog, owed.Value, Schedule, next, now, cancellationToken);
        if (started is null)
            await MoveNextRunAsync(digest.Id, next, cancellationToken);
        else
            LogStarted(logger, digest.Id, owed.Value, missed ? "missed" : "started");

        return missed ? null : started;
    }

    /// <summary>Starts a run of a digest now, outside its schedule.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="catalog">Its catalog.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The run; none when a run of the same instant exists.</returns>
    public async Task<Guid?> StartNowAsync(DigestEntity digest, CatalogEntity catalog, CancellationToken cancellationToken)
    {
        var now = time.StoredUtcNow();
        return await ClaimAsync(digest, catalog, now, Manual, nextRunAt: null, now, cancellationToken);
    }

    /// <summary>Renders a digest now without writing: what <see cref="StartNowAsync"/> would send at this instant.</summary>
    public Task<DigestDraft> PreviewAsync(DigestEntity digest, CatalogEntity catalog, CancellationToken cancellationToken)
    {
        var now = time.StoredUtcNow();
        return drafter.DraftAsync(digest, catalog, now, now, cancellationToken);
    }

    /// <summary>Sends a run's pending deliveries that are due, holding the run's lease meanwhile; finishes the run when every
    /// recipient has a final result. A run another host holds sends nothing.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public async Task SendAsync(Guid runId, CancellationToken cancellationToken)
    {
        var now = time.StoredUtcNow();
        var lease = now + options.Value.LeaseDuration;
        var claimed = await db.DigestRuns
            .Where(run => run.Id == runId && run.Status == DigestRows.Running && (run.LeaseUntil == null || run.LeaseUntil <= now))
            .ExecuteUpdateAsync(set => set.SetProperty(run => run.LeaseUntil, lease), cancellationToken);
        if (claimed == 0)
            return;

        var row = await db.DigestRuns.FirstAsync(run => run.Id == runId, cancellationToken);
        var digest = await db.Digests.AsNoTracking().FirstAsync(item => item.Id == row.DigestId, cancellationToken);
        var catalog = await db.Catalogs.AsNoTracking().Where(item => item.Id == digest.CatalogId).Select(item => item.Slug).FirstAsync(cancellationToken);
        var texts = EventsJson.Read<List<DigestTextModel>>(row.Texts).ToDictionary(text => text.Language, text => text.Text, StringComparer.Ordinal);
        var items = EventsJson.Read<List<DigestItemModel>>(row.Items);
        var channel = deliveries.FirstOrDefault(delivery => delivery.Channel == digest.Channel);
        var pending = await db.DigestDeliveries
            .Where(delivery => delivery.RunId == runId && delivery.Status == DigestRecipientStatusConstants.Pending
                && (delivery.NextAttemptAt == null || delivery.NextAttemptAt <= now))
            .OrderBy(delivery => delivery.Ordinal)
            .ToListAsync(cancellationToken);

        foreach (var delivery in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = channel is null
                ? DigestDeliveryOutcome.Failed($"channel '{digest.Channel}' is not registered on this host")
                : await DeliverAsync(channel, new DigestMessage
                {
                    DigestId = digest.Id,
                    RunId = runId,
                    Catalog = catalog,
                    Issuer = digest.Issuer,
                    OrganizationId = digest.OrganizationId,
                    Subject = delivery.Subject,
                    Language = delivery.Language,
                    Text = texts.GetValueOrDefault(delivery.Language) ?? texts.Values.First(),
                    Items = items,
                    IdempotencyKey = IdempotencyKey(runId, delivery.Subject),
                }, cancellationToken);
            Record(delivery, outcome, time.StoredUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }

        var open = await db.DigestDeliveries
            .Where(delivery => delivery.RunId == runId && delivery.Status == DigestRecipientStatusConstants.Pending)
            .Select(delivery => delivery.NextAttemptAt)
            .ToListAsync(cancellationToken);
        if (open.Count > 0)
        {
            // Released until the earliest retry is due; the scheduler resumes it then.
            row.LeaseUntil = open.Min() ?? time.StoredUtcNow();
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        await FinishAsync(row, cancellationToken);
    }

    /// <summary>Builds the key a recipient's message is sent once per: the run and the person, printable ASCII.</summary>
    public static string IdempotencyKey(Guid runId, string subject)
    {
        var key = $"events-digest:{runId:N}:{subject}";
        if (key.Length <= MaxIdempotencyKeyLength && key.All(character => character is >= '!' and <= '~'))
            return key;

        return $"events-digest:{runId:N}:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(subject)))[..32]}";
    }

    private async Task<DigestDeliveryOutcome> DeliverAsync(IDigestDelivery channel, DigestMessage message, CancellationToken cancellationToken)
    {
        try
        {
            return await channel.DeliverAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogDeliveryFailed(logger, message.DigestId, message.RunId, channel.Channel, exception);
            return DigestDeliveryOutcome.Failed($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private void Record(DigestDeliveryEntity delivery, DigestDeliveryOutcome outcome, DateTimeOffset now)
    {
        delivery.Attempts++;
        delivery.UpdatedAt = now;
        delivery.Error = outcome.Error is { Length: > MaxErrorLength } error ? error[..MaxErrorLength] : outcome.Error;
        var retry = outcome.Status == DigestRecipientStatusConstants.Failed && outcome.RetryAfter is { } wait && delivery.Attempts < options.Value.MaxAttempts;
        delivery.Status = retry ? DigestRecipientStatusConstants.Pending : Final(outcome.Status);
        delivery.NextAttemptAt = retry ? StoredClock.Truncate(now + Clamp(outcome.RetryAfter!.Value)) : null;
    }

    private static TimeSpan Clamp(TimeSpan wait) =>
        wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : wait > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : wait;

    private static string Final(string status) => status switch
    {
        DigestRecipientStatusConstants.Sent or DigestRecipientStatusConstants.NotLinked or DigestRecipientStatusConstants.Muted => status,
        _ => DigestRecipientStatusConstants.Failed,
    };

    private async Task FinishAsync(DigestRunEntity row, CancellationToken cancellationToken)
    {
        var statuses = await db.DigestDeliveries.Where(delivery => delivery.RunId == row.Id).Select(delivery => delivery.Status).ToListAsync(cancellationToken);
        var failed = statuses.Count(status => status == DigestRecipientStatusConstants.Failed);
        row.Status = EventsLabels.Of(statuses.Count == 0 || failed == statuses.Count
            ? DigestRunStatus.Failed
            : failed > 0 ? DigestRunStatus.Partial : DigestRunStatus.Completed);
        row.LeaseUntil = null;
        row.FinishedAt = time.StoredUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        LogFinished(logger, row.DigestId, row.Id, row.Status, statuses.Count, failed);
    }

    /// <summary>Claims an occurrence: renders it, then writes the run, its deliveries and the digest's next run in one
    /// transaction. A run with nothing to send (no event, no recipient) finishes in the same write.</summary>
    private async Task<Guid?> ClaimAsync(
        DigestEntity digest, CatalogEntity catalog, DateTimeOffset occurrence, string trigger, DateTimeOffset? nextRunAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var draft = await drafter.DraftAsync(digest, catalog, now, now, cancellationToken);
        var notes = draft.Notes.ToList();
        var status = DigestRunStatus.Running;
        if (draft.Items.Count == 0)
        {
            status = DigestRunStatus.Empty;
        }
        else if (draft.Recipients.Count == 0)
        {
            status = DigestRunStatus.Failed;
            notes.Add("No recipient could be read.");
        }

        var run = new DigestRunEntity
        {
            Id = Guid.CreateVersion7(now),
            DigestId = digest.Id,
            OccurrenceAt = occurrence,
            Trigger = trigger,
            Status = EventsLabels.Of(status),
            Matched = draft.Matched,
            Selected = draft.Items.Count,
            EventIds = EventsJson.Write(draft.Items.Select(item => item.EventId).ToList()),
            Items = EventsJson.Write(draft.Items),
            Texts = EventsJson.Write(draft.Texts),
            Notes = EventsJson.Write(notes),
            StartedAt = now,
            FinishedAt = status == DigestRunStatus.Running ? null : now,
        };

        return await InsertAsync(digest.Id, run, status == DigestRunStatus.Running ? draft.Recipients : [], nextRunAt, now, cancellationToken) ? run.Id : null;
    }

    private async Task<Guid?> ClaimMissedAsync(DigestEntity digest, DateTimeOffset occurrence, DateTimeOffset? nextRunAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var run = new DigestRunEntity
        {
            Id = Guid.CreateVersion7(now),
            DigestId = digest.Id,
            OccurrenceAt = occurrence,
            Trigger = Schedule,
            Status = EventsLabels.Of(DigestRunStatus.Missed),
            Notes = EventsJson.Write(new List<string> { $"The host was down past the catch-up window ({options.Value.CatchUpWindow}); nothing was sent." }),
            StartedAt = now,
            FinishedAt = now,
        };

        return await InsertAsync(digest.Id, run, [], nextRunAt, now, cancellationToken) ? run.Id : null;
    }

    private async Task<bool> InsertAsync(
        Guid digestId, DigestRunEntity run, IReadOnlyList<(string Subject, string Language)> recipients, DateTimeOffset? nextRunAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.DigestRuns.Add(run);
        try
        {
            // The run first: the model maps no relationship, so one save would not order the inserts.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (StoreErrors.IsUniqueViolation(exception))
        {
            // Another host, or this one before a restart, claimed the occurrence.
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return false;
        }

        db.DigestDeliveries.AddRange(recipients.Select((recipient, index) => new DigestDeliveryEntity
        {
            RunId = run.Id,
            Subject = recipient.Subject,
            Ordinal = index,
            Language = recipient.Language,
            Status = DigestRecipientStatusConstants.Pending,
            UpdatedAt = now,
        }));
        await db.SaveChangesAsync(cancellationToken);

        if (run.Trigger == Schedule)
            await db.Digests.Where(row => row.Id == digestId).ExecuteUpdateAsync(set => set.SetProperty(row => row.NextRunAt, nextRunAt), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return true;
    }

    private async Task MoveNextRunAsync(Guid digestId, DateTimeOffset? next, CancellationToken cancellationToken) =>
        await db.Digests.Where(row => row.Id == digestId).ExecuteUpdateAsync(set => set.SetProperty(row => row.NextRunAt, next), cancellationToken);

    [LoggerMessage(EventId = 4301, Level = LogLevel.Information, Message = "Events digest {DigestId}: occurrence {Occurrence} {Outcome}.")]
    private static partial void LogStarted(ILogger logger, Guid digestId, DateTimeOffset occurrence, string outcome);

    [LoggerMessage(EventId = 4302, Level = LogLevel.Information, Message = "Events digest {DigestId} run {RunId} finished {Status}: {Recipients} recipient(s), {Failed} failed.")]
    private static partial void LogFinished(ILogger logger, Guid digestId, Guid runId, string status, int recipients, int failed);

    [LoggerMessage(EventId = 4303, Level = LogLevel.Warning, Message = "Events digest {DigestId} run {RunId}: channel {Channel} threw; the recipient reads failed.")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid digestId, Guid runId, string channel, Exception exception);
}
