using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Integrates the caller organization's tracking of a catalog's events, in process or over the service.</summary>
/// <remarks>Every call acts for the current principal's active organization and sees its tracking only; a call without
/// one fails with <see cref="EventsErrors.OrganizationRequired"/>. Tracking never changes the event.</remarks>
public interface IEventTrackingBroker
{
    /// <summary>Lists a catalog's published events with the organization's tracking.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="query">The filters and the page.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>One page of tracked events.</returns>
    Task<Result<TrackingPage>> ListAsync(string catalog, TrackingQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets one event with the organization's tracking.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The event and its tracking; <see cref="EventsErrors.EventNotFound"/> when the caller cannot see it.</returns>
    Task<Result<TrackedEventModel>> GetAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Starts or changes the organization's tracking of an event; a stage change lands in its history.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="spec">The tracking's fields.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The tracking; <see cref="EventsErrors.StageUnknown"/> for a stage outside the workflow;
    /// <see cref="EventsErrors.RevisionConflict"/> when the revision is stale.</returns>
    Task<Result<TrackingModel>> PutAsync(string catalog, Guid eventId, TrackingSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Stops tracking an event; its history goes with it.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The removed tracking; <see cref="EventsErrors.TrackingNotFound"/> when there is none.</returns>
    Task<Result<TrackingModel>> DeleteAsync(string catalog, Guid eventId, int expectedRevision, CancellationToken cancellationToken = default);

    /// <summary>Lists the stage changes of the organization's tracking of an event, oldest first.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The history.</returns>
    Task<Result<IReadOnlyList<TrackingHistoryEntryModel>>> GetHistoryAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Counts the organization's tracking per stage and lists its nearest open deadlines.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="today">The first day a deadline may fall on, in the caller's own calendar.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The summary, with at most five deadlines.</returns>
    Task<Result<TrackingSummaryModel>> GetSummaryAsync(string catalog, DateOnly today, CancellationToken cancellationToken = default);
}
