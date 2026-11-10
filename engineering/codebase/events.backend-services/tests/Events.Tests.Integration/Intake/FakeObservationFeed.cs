using System.Collections.Concurrent;
using System.Globalization;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Integration.Intake;

/// <summary>Stands in for SCFD's pull cursor: one append-only log per subscription, read from an offset cursor, so
/// reading an older cursor replays as SCFD does.</summary>
internal sealed class FakeObservationFeed : IObservationFeed
{
    private readonly ConcurrentDictionary<string, List<ScfdDeliveryRecord>> logs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Exception> failures = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ScfdSubscription> subscriptions = new(StringComparer.Ordinal);

    /// <summary>Gets every read, as (source, cursor), in order.</summary>
    public ConcurrentQueue<(string Source, string? Cursor)> Reads { get; } = new();

    /// <summary>Appends records to a source's subscription log.</summary>
    public void Append(EventsIntakeSourceOptions source, params ScfdDeliveryRecord[] records)
    {
        var log = logs.GetOrAdd(IntakeSources.Subscription(source), _ => []);
        lock (log)
            log.AddRange(records);
    }

    /// <summary>Makes every read of a source fail, or succeed again with none.</summary>
    public void Fail(EventsIntakeSourceOptions source, Exception? failure)
    {
        if (failure is null)
            failures.TryRemove(source.Name, out _);
        else
            failures[source.Name] = failure;
    }

    /// <summary>Sets what a source's subscription answers; by default it matches the source: its filter, the schema, active.</summary>
    public void Subscription(EventsIntakeSourceOptions source, ScfdSubscription subscription) =>
        subscriptions[IntakeSources.Subscription(source)] = subscription;

    /// <inheritdoc />
    public Task<ScfdSubscription> ReadSubscriptionAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken)
    {
        if (failures.TryGetValue(source.Name, out var failure))
            throw failure;

        return Task.FromResult(subscriptions.TryGetValue(IntakeSources.Subscription(source), out var set)
            ? set
            : new ScfdSubscription(ScfdSubscription.ExpectedFilter(source), EventsConstants.EventSchema, ScfdSubscription.ActiveState));
    }

    /// <inheritdoc />
    public Task<ObservationFeedPage> ReadAsync(EventsIntakeSourceOptions source, string? cursor, int limit, CancellationToken cancellationToken)
    {
        Reads.Enqueue((source.Name, cursor));
        if (failures.TryGetValue(source.Name, out var failure))
            throw failure;

        var log = logs.GetOrAdd(IntakeSources.Subscription(source), _ => []);
        lock (log)
        {
            var offset = cursor is null ? 0 : int.Parse(cursor, CultureInfo.InvariantCulture);
            var records = log.Skip(offset).Take(limit).ToList();
            var next = offset + records.Count;
            return Task.FromResult(new ObservationFeedPage(records, next.ToString(CultureInfo.InvariantCulture), next < log.Count));
        }
    }
}
