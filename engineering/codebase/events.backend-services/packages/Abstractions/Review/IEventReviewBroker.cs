using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Integrates a catalog's review — the queue, candidates, source changes and observations — in process or over
/// the service.</summary>
/// <remarks>Every call acts for the current principal and needs the catalog's owner organization. Nothing a source
/// reports publishes without one of these calls.</remarks>
public interface IEventReviewBroker
{
    /// <summary>Lists the review queue.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="query">The page.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>One page of the queue.</returns>
    Task<Result<ReviewPage>> ListAsync(string catalog, ReviewQuery query, CancellationToken cancellationToken = default);

    /// <summary>Publishes a candidate, with its proposed facts or the reviewer's edits.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The candidate.</param>
    /// <param name="decision">The revision read and optional edits.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The published event; <see cref="EventsErrors.ReviewStateInvalid"/> unless it is a candidate.</returns>
    Task<Result<EventModel>> AcceptAsync(string catalog, Guid id, ReviewDecision decision, CancellationToken cancellationToken = default);

    /// <summary>Rejects a candidate; later revisions of its posts stay evidence only.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The candidate.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The rejected event.</returns>
    Task<Result<EventModel>> RejectAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default);

    /// <summary>Returns a published event to the review queue as a candidate: it leaves every reader's list until a
    /// reviewer accepts it again. Reverses an automatic accept (events-integration #53).</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The published event.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The candidate; <see cref="EventsErrors.ReviewStateInvalid"/> unless it is published.</returns>
    Task<Result<EventModel>> ReopenAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default);

    /// <summary>Merges a candidate into another event: its sources and observations move, the candidate archives.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The candidate.</param>
    /// <param name="decision">The target and both revisions read.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The target event, with the candidate's sources.</returns>
    Task<Result<EventModel>> MergeAsync(string catalog, Guid id, MergeDecision decision, CancellationToken cancellationToken = default);

    /// <summary>Applies a pending source change to its published event, with its proposed facts or the reviewer's edits.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="observationId">The pending observation.</param>
    /// <param name="decision">The event's revision read and optional edits.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The changed event.</returns>
    Task<Result<EventModel>> ApplyChangeAsync(string catalog, Guid observationId, ReviewDecision decision, CancellationToken cancellationToken = default);

    /// <summary>Dismisses a pending source change; the event stays as reviewed.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="observationId">The pending observation.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The dismissed observation.</returns>
    Task<Result<ObservationModel>> DismissChangeAsync(string catalog, Guid observationId, CancellationToken cancellationToken = default);

    /// <summary>Lists a catalog's observations, quarantined ones included.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="query">The filters and the page.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>One page of observations.</returns>
    Task<Result<ObservationPage>> ListObservationsAsync(string catalog, ObservationQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets an observation with its payload.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The observation.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The observation.</returns>
    Task<Result<ObservationModel>> GetObservationAsync(string catalog, Guid id, CancellationToken cancellationToken = default);
}
