using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Catalogs;

/// <summary>Integrates the running Events service's catalogs.</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiEventCatalogsBroker(EventsApiTransport transport) : IEventCatalogsBroker
{
    /// <inheritdoc />
    public Task<Result<CatalogModel>> GetAsync(string slug, CancellationToken cancellationToken = default) =>
        transport.SendAsync<CatalogModel>(HttpMethod.Get, EventsApiRoutes.Catalog(slug), cancellationToken);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CatalogModel>>> ListAsync(CancellationToken cancellationToken = default) =>
        transport.SendAsync<IReadOnlyList<CatalogModel>>(HttpMethod.Get, EventsApiRoutes.Catalogs(), cancellationToken);

    /// <inheritdoc />
    public Task<Result<CatalogModel>> PutAsync(string slug, CatalogSpec spec, CancellationToken cancellationToken = default) =>
        transport.SendAsync<CatalogSpec, CatalogModel>(HttpMethod.Put, EventsApiRoutes.Catalog(slug), spec, cancellationToken);
}
