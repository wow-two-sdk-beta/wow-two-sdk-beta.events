using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Integrates the intake's operations in process: every configured source's freshness, what waits for review,
/// and an on-demand poll. Without <c>AddEventsIntake</c> no source is configured, and the status lists none.</summary>
/// <param name="db">The module's store.</param>
/// <param name="options">The intake's settings, read per scope.</param>
/// <param name="poller">Polls one source; registered by <c>AddEventsIntake</c>.</param>
internal sealed class ModuleIntakeBroker(EventsDbContext db, IOptionsSnapshot<EventsIntakeOptions> options, IntakeSourcePoller? poller = null) : IEventIntakeBroker
{
    private static readonly string Candidate = EventsLabels.Of(EventReviewState.Candidate);
    private static readonly string Pending = EventsLabels.Of(ObservationState.Pending);

    /// <inheritdoc />
    public async Task<Result<IntakeStatusModel>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var sources = options.Value.Sources;
        var names = sources.Select(source => source.Name).ToList();
        var cursors = await db.IntakeCursors.AsNoTracking()
            .Where(row => names.Contains(row.SourceName))
            .ToDictionaryAsync(row => row.SourceName, StringComparer.Ordinal, cancellationToken);

        var candidates = db.Events.AsNoTracking().Where(row => row.Review == Candidate);
        var pending = db.Observations.AsNoTracking().Where(row => row.State == Pending);
        var oldestCandidate = await candidates.MinAsync(row => (DateTimeOffset?)row.CreatedAt, cancellationToken);
        var oldestPending = await pending.MinAsync(row => (DateTimeOffset?)row.ReceivedAt, cancellationToken);

        return Result<IntakeStatusModel>.Ok(new IntakeStatusModel
        {
            Sources = sources.Select(source =>
            {
                var row = cursors.GetValueOrDefault(source.Name);
                return new IntakeSourceStatusModel
                {
                    Name = source.Name,
                    Catalog = source.Catalog,
                    SubscriptionId = IntakeSources.Subscription(source),
                    Enabled = source.Enabled,
                    Cursor = row?.Cursor,
                    LastPolledAtUtc = row?.LastPolledAt,
                    LastReceivedAtUtc = row?.LastReceivedAt,
                    NewestObservedAtUtc = row?.NewestObservedAt,
                    LastError = row?.LastError,
                    ConsecutiveFailures = row?.ConsecutiveFailures ?? 0,
                    Received = row?.Received ?? 0,
                    Quarantined = row?.Quarantined ?? 0,
                };
            }).ToList(),
            Candidates = await candidates.LongCountAsync(cancellationToken),
            PendingChanges = await pending.LongCountAsync(cancellationToken),
            OldestUnresolvedAtUtc = (oldestCandidate, oldestPending) switch
            {
                ({ } candidate, { } change) => candidate < change ? candidate : change,
                var (candidate, change) => candidate ?? change,
            },
        });
    }

    /// <inheritdoc />
    public async Task<Result<IntakeStatusModel>> PollAsync(CancellationToken cancellationToken = default)
    {
        // One source after another, never in parallel, so two subscriptions never race on one post.
        if (poller is not null)
        {
            foreach (var source in options.Value.Sources.Where(source => source.Enabled))
                await poller.PollAsync(source, cancellationToken);
        }

        return await GetStatusAsync(cancellationToken);
    }
}
