using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Review;

/// <summary>Integrates the running Events service's review queue and observations.</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiEventReviewBroker(EventsApiTransport transport) : IEventReviewBroker
{
    /// <inheritdoc />
    public Task<Result<ReviewPage>> ListAsync(string catalog, ReviewQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var route = new EventsApiQuery().Add("cursor", query.Cursor).Add("limit", query.Limit)
            .ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Review));
        return transport.SendAsync<ReviewPage>(HttpMethod.Get, route, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<EventModel>> AcceptAsync(string catalog, Guid id, ReviewDecision decision, CancellationToken cancellationToken = default) =>
        transport.SendAsync<ReviewDecision, EventModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Review, id, "accept"), decision, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> RejectAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventModel>(
            HttpMethod.Post,
            new EventsApiQuery().Add("expectedRevision", expectedRevision).ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Review, id, "reject")),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> ReopenAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventModel>(
            HttpMethod.Post,
            new EventsApiQuery().Add("expectedRevision", expectedRevision).ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Review, id, "reopen")),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> MergeAsync(string catalog, Guid id, MergeDecision decision, CancellationToken cancellationToken = default) =>
        transport.SendAsync<MergeDecision, EventModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Review, id, "merge"), decision, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> ApplyChangeAsync(string catalog, Guid observationId, ReviewDecision decision, CancellationToken cancellationToken = default) =>
        transport.SendAsync<ReviewDecision, EventModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Observations, observationId, "apply"), decision, cancellationToken);

    /// <inheritdoc />
    public Task<Result<ObservationModel>> DismissChangeAsync(string catalog, Guid observationId, CancellationToken cancellationToken = default) =>
        transport.SendAsync<ObservationModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Observations, observationId, "dismiss"), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ObservationPage>> ListObservationsAsync(string catalog, ObservationQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var route = new EventsApiQuery()
            .AddEnum("state", query.State)
            .Add("eventId", query.EventId)
            .Add("cursor", query.Cursor)
            .Add("limit", query.Limit)
            .ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Observations));
        return transport.SendAsync<ObservationPage>(HttpMethod.Get, route, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<ObservationModel>> GetObservationAsync(string catalog, Guid id, CancellationToken cancellationToken = default) =>
        transport.SendAsync<ObservationModel>(HttpMethod.Get, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Observations, id), cancellationToken);
}
