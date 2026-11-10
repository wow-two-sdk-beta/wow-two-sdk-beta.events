namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Defines where intake reads records from: SCFD's pull cursor in production, a fake in tests.</summary>
internal interface IObservationFeed
{
    /// <summary>Reads one page from a cursor; reading an older cursor again replays.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cursor">The committed cursor; none starts at the log's beginning.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The page; a failed read throws.</returns>
    Task<ObservationFeedPage> ReadAsync(EventsIntakeSourceOptions source, string? cursor, int limit, CancellationToken cancellationToken);

    /// <summary>Reads the source's subscription: its filter, schema and state.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The subscription; a failed read throws.</returns>
    Task<ScfdSubscription> ReadSubscriptionAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken);
}
