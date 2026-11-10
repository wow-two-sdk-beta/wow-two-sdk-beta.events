using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.CatalogEvents;

/// <summary>Integrates the running Events service's catalog events.</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiCatalogEventsBroker(EventsApiTransport transport) : ICatalogEventsBroker
{
    /// <inheritdoc />
    public Task<Result<EventPage>> ListAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventPage>(HttpMethod.Get, EventQueryMapper.ToQuery(query).ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Events)), cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventModel>(HttpMethod.Get, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Events, id), cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> CreateAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventSpec, EventModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Events), spec, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> ProposeAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventSpec, EventModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Candidates), spec, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> UpdateAsync(string catalog, Guid id, EventSpec spec, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventSpec, EventModel>(HttpMethod.Put, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Events, id), spec, cancellationToken);

    /// <inheritdoc />
    public Task<Result<EventModel>> ArchiveAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventModel>(
            HttpMethod.Delete,
            new EventsApiQuery().Add("expectedRevision", expectedRevision).ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Events, id)),
            cancellationToken);
}
