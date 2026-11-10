using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Review;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Consumes one page of a source: its observations, source posts, event changes and the next cursor commit in one
/// transaction (events-integration #15, #19), so a crash before the commit replays the page.</summary>
/// <remarks>
///   - a retry is a no-op: a delivery already stored under (instance, subscription, <c>EventId</c>) is skipped
///   - a post is one identity per SCFD instance, whichever subscription delivers it; its highest revision wins
///   - a malformed record is stored as quarantined and never fails the page; nothing publishes unreviewed
/// </remarks>
/// <param name="db">The module's store, in the page's own scope.</param>
/// <param name="feed">Where records come from.</param>
/// <param name="time">The clock rows are stamped with.</param>
/// <param name="hooks">The commit seam; none in production.</param>
internal sealed class IntakeProcessor(EventsDbContext db, IObservationFeed feed, TimeProvider time, IEnumerable<IIntakeCommitHook> hooks)
{
    private const string CursorTable = EventsModuleConstants.SchemaName + ".events_intake_cursors";
    private const string EnsureCursorSql =
        "INSERT INTO " + CursorTable + " (source_name, subscription_id, consecutive_failures, received, quarantined, updated_at) " +
        "VALUES ({0}, {1}, 0, 0, 0, {2}) ON CONFLICT (source_name) DO NOTHING";
    private const string LockCursorSql = "SELECT * FROM " + CursorTable + " WHERE source_name = {0} FOR UPDATE";
    private const string NotPrivate = "catalog not private";

    private static readonly HashSet<string> PostTypes = new(StringComparer.Ordinal) { "post.new", "post.changed", "post.extracted", "post.relisted" };
    private static readonly string Scfd = EventsLabels.Of(EventSourceKind.Scfd);
    private static readonly string Private = EventsLabels.Of(CatalogVisibility.Private);
    private static readonly string Candidate = EventsLabels.Of(EventReviewState.Candidate);
    private static readonly string Published = EventsLabels.Of(EventReviewState.Published);
    private static readonly string Applied = EventsLabels.Of(ObservationState.Applied);
    private static readonly string Pending = EventsLabels.Of(ObservationState.Pending);
    private static readonly string Superseded = EventsLabels.Of(ObservationState.Superseded);
    private static readonly string Quarantined = EventsLabels.Of(ObservationState.Quarantined);
    private static readonly string Recorded = EventsLabels.Of(ObservationState.Recorded);
    private static readonly string Ignored = EventsLabels.Of(ObservationState.Ignored);

    /// <summary>Reads one page from the committed cursor and commits what it delivered with the next cursor.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>What the page did; a failure when the source's catalog is missing. A failed read or write throws, and
    /// the transaction rolls back.</returns>
    public async Task<Result<IntakePageOutcome>> ProcessPageAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken)
    {
        var subscription = IntakeSources.Subscription(source);
        var catalog = await db.Catalogs.AsNoTracking().FirstOrDefaultAsync(row => row.Slug == source.Catalog, cancellationToken);
        if (catalog is null)
            return Result<IntakePageOutcome>.Fail(AppErrorFactory.NotFound($"The catalog '{source.Catalog}' of intake source '{source.Name}' was not found."));

        var committed = await db.IntakeCursors.AsNoTracking().FirstOrDefaultAsync(row => row.SourceName == source.Name, cancellationToken);
        var cursor = committed is not null && committed.SubscriptionId == subscription ? committed.Cursor : null;
        var page = await feed.ReadAsync(source, cursor, source.PageSize, cancellationToken);

        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(EnsureCursorSql, [source.Name, subscription, now], cancellationToken);
        var row = (await RelationalQueryableExtensions.FromSqlRaw(db.IntakeCursors, LockCursorSql, source.Name).ToListAsync(cancellationToken)).Single();

        // Another poller committed this page meanwhile: leave it, and read on from the cursor it committed.
        if ((row.SubscriptionId == subscription ? row.Cursor : null) != cursor)
            return Result<IntakePageOutcome>.Ok(new IntakePageOutcome(0, 0, HasMore: true));

        var state = new IntakePageState(source, subscription, catalog, now);
        var deliveries = page.Records.Where(record => record.EventId is not null).Select(record => record.EventId).Distinct().ToList();
        var seen = (await db.Observations.AsNoTracking()
                .Where(observation => observation.SourceInstance == source.Instance && observation.SubscriptionId == subscription
                    && deliveries.Contains(observation.DeliveryEventId))
                .Select(observation => observation.DeliveryEventId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var received = 0;
        var quarantined = 0;
        DateTimeOffset? newest = null;
        foreach (var record in page.Records)
        {
            if (record.EventId is { } delivery && !seen.Add(delivery))
                continue;

            var observation = await ProcessAsync(state, record, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            received++;
            quarantined += observation.State == Quarantined ? 1 : 0;
            newest = newest is null || observation.ObservedAt > newest ? observation.ObservedAt : newest;
        }

        row.SubscriptionId = subscription;
        row.Cursor = page.Next ?? cursor;
        row.LastPolledAt = now;
        row.LastReceivedAt = received > 0 ? now : row.LastReceivedAt;
        row.NewestObservedAt = newest is not null && (row.NewestObservedAt is null || newest > row.NewestObservedAt) ? newest : row.NewestObservedAt;
        row.LastError = null;
        row.ConsecutiveFailures = 0;
        row.Received += received;
        row.Quarantined += quarantined;
        row.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        foreach (var hook in hooks)
            await hook.BeforeCommitAsync(source, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<IntakePageOutcome>.Ok(new IntakePageOutcome(received, quarantined, page.HasMore));
    }

    private async Task<ObservationEntity> ProcessAsync(IntakePageState page, ScfdDeliveryRecord record, CancellationToken cancellationToken)
    {
        var data = record.Data is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } value ? value : (JsonElement?)null;
        var observation = new ObservationEntity
        {
            Id = Guid.CreateVersion7(page.Now),
            CatalogId = page.Catalog.Id,
            SourceKind = Scfd,
            SourceInstance = page.Source.Instance,
            SubscriptionId = page.Subscription,
            PipelineId = Fit(record.PipelineId?.ToString("D") ?? page.Source.PipelineId, 64),
            DeliveryEventId = record.EventId,
            ItemId = Fit(record.ItemId, 128),
            Type = Fit(record.Type, 64) ?? "unknown",
            Reason = Fit(record.Reason, 32),
            PostId = record.PostId ?? ScfdEventMapper.PostId(data),
            Revision = record.Revision ?? ScfdEventMapper.Revision(data),
            ObservedAt = (record.OccurredAtUtc ?? page.Now).ToUniversalTime(),
            ReceivedAt = page.Now,
            MappingVersion = ScfdEventMapper.MappingVersion(data),
            Links = EventsJson.Write(ScfdEventMapper.Links(data)),
            Payload = EventsJson.Write(data),
            State = Recorded,
        };
        db.Observations.Add(observation);

        if (record.EventId is null)
            Quarantine(observation, "no delivery id");
        else if (page.Catalog.Visibility != Private)
            Quarantine(observation, NotPrivate);
        else if (PostTypes.Contains(observation.Type))
            await ApplyPostAsync(page, observation, data, cancellationToken);
        else
            await RecordAsync(page, observation, cancellationToken);

        return observation;
    }

    private async Task ApplyPostAsync(IntakePageState page, ObservationEntity observation, JsonElement? data, CancellationToken cancellationToken)
    {
        if (data is not { } payload)
        {
            Quarantine(observation, "no payload");
            return;
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            Quarantine(observation, "the payload is not an object");
            return;
        }

        if (ScfdEventMapper.Schema(payload) is var schema && schema != EventsConstants.EventSchema)
        {
            Quarantine(observation, $"schema '{schema ?? "none"}' is not {EventsConstants.EventSchema}");
            return;
        }

        if (observation.PostId is not { } postId)
        {
            Quarantine(observation, "no post id");
            return;
        }

        if (observation.Revision is not { } revision)
        {
            Quarantine(observation, "no revision");
            return;
        }

        if (!ScfdEventMapper.TryMap(payload, page.Kinds, out var mapped, out var reason))
        {
            Quarantine(observation, reason);
            return;
        }

        if (EventFactsValidator.Validate(mapped, ProposedFacts.SourcedKinds(page.Kinds), out var facts) is { } invalid)
        {
            Quarantine(observation, invalid.Message);
            return;
        }

        observation.Proposed = EventsJson.Write(facts);
        var instance = page.Source.Instance;
        var post = await db.SourcePosts.FirstOrDefaultAsync(row => row.SourceInstance == instance && row.PostId == postId, cancellationToken);
        if (post is not null && revision <= post.LatestRevision)
        {
            observation.State = Superseded;
            observation.EventId = post.EventId;
            return;
        }

        var sourceId = $"{instance}/{postId:D}";
        var url = ScfdEventMapper.Links(payload).Select(link => link.Url).FirstOrDefault(link => !string.IsNullOrEmpty(link));
        var identity = await db.Sources.FirstOrDefaultAsync(row => row.CatalogId == page.Catalog.Id && row.Kind == Scfd && row.SourceId == sourceId, cancellationToken);
        if (post is null)
        {
            post = new SourcePostEntity { SourceInstance = instance, PostId = postId, CatalogId = page.Catalog.Id, EventId = identity?.EventId };
            db.SourcePosts.Add(post);
        }

        post.SubscriptionId = page.Subscription;
        post.LatestRevision = revision;
        post.LatestObservationId = observation.Id;
        post.UpdatedAt = page.Now;

        if (post.EventId is null)
        {
            post.EventId = await CreateCandidateAsync(page, facts, sourceId, url, cancellationToken);
            observation.State = Applied;
            observation.EventId = post.EventId;
            return;
        }

        if (identity is not null)
        {
            identity.LastSeenAt = page.Now;
            identity.Url = url ?? identity.Url;
        }

        var target = await db.Events.FirstOrDefaultAsync(row => row.Id == post.EventId, cancellationToken);
        observation.EventId = post.EventId;
        if (target?.Review == Candidate)
        {
            // A newer revision of an unreviewed candidate updates it in place.
            await EventWrites.ApplyAsync(db, target, facts, isNew: false, cancellationToken);
            target.Revision++;
            target.UpdatedAt = page.Now;
            observation.State = Applied;
        }
        else if (target?.Review == Published)
        {
            // A reviewed event never changes by itself: the newest revision waits as the one pending change of its post.
            await db.Observations
                .Where(row => row.SourceInstance == instance && row.PostId == postId && row.State == Pending)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.State, Superseded), cancellationToken);
            observation.State = Pending;
        }
        else
        {
            observation.State = Ignored;
        }
    }

    private async Task RecordAsync(IntakePageState page, ObservationEntity observation, CancellationToken cancellationToken)
    {
        observation.State = Recorded;
        if (observation.PostId is not { } postId)
            return;

        var instance = page.Source.Instance;
        observation.EventId = await db.SourcePosts.AsNoTracking()
            .Where(row => row.SourceInstance == instance && row.PostId == postId)
            .Select(row => row.EventId)
            .FirstOrDefaultAsync(cancellationToken);

        if (observation.Type == "post.purged")
        {
            // A purge is honoured: the post's delivered payloads go, the rows and every reviewed fact stay.
            observation.Payload = null;
            await db.Observations
                .Where(row => row.SourceInstance == instance && row.PostId == postId && row.Payload != null)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.Payload, (string?)null), cancellationToken);
        }
    }

    private async Task<Guid> CreateCandidateAsync(IntakePageState page, EventFacts facts, string sourceId, string? url, CancellationToken cancellationToken)
    {
        var row = new CatalogEventEntity
        {
            Id = Guid.CreateVersion7(page.Now),
            CatalogId = page.Catalog.Id,
            Review = Candidate,
            Revision = 1,
            CreatedAt = page.Now,
            UpdatedAt = page.Now,
        };
        await EventWrites.ApplyAsync(db, row, facts, isNew: true, cancellationToken);
        db.Events.Add(row);
        db.Sources.Add(new EventSourceEntity
        {
            CatalogId = page.Catalog.Id,
            Kind = Scfd,
            SourceId = sourceId,
            EventId = row.Id,
            Url = url,
            FirstSeenAt = page.Now,
            LastSeenAt = page.Now,
        });
        return row.Id;
    }

    private static void Quarantine(ObservationEntity observation, string reason)
    {
        observation.State = Quarantined;
        observation.QuarantineReason = reason;
    }

    private static string? Fit(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Length > max ? text[..max] : text;
}
