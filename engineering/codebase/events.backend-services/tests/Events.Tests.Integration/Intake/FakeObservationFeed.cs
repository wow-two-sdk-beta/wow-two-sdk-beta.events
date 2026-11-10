using System.Collections.Concurrent;
using System.Globalization;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Integration.Intake;

/// <summary>Stands in for SCFD's pull cursor: one append-only log per subscription, read from an offset cursor, so
/// reading an older cursor replays as SCFD does.</summary>
internal sealed class FakeObservationFeed : IObservationFeed
{
    private readonly ConcurrentDictionary<string, List<ScfdDeliveryRecord>> logs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Exception> failures = new(StringComparer.Ordinal);

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
