using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Imports;

/// <summary>Integrates the running Events service's imports; each row's <c>legacy</c> evidence passes through as written.</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiEventImportsBroker(EventsApiTransport transport) : IEventImportsBroker
{
    /// <inheritdoc />
    public Task<Result<EventImportReport>> ImportAsync(string catalog, EventImportBundle bundle, bool dryRun, CancellationToken cancellationToken = default) =>
        transport.SendAsync<EventImportBundle, EventImportReport>(
            HttpMethod.Post,
            new EventsApiQuery().Add("dryRun", dryRun).ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Imports)),
            bundle,
            cancellationToken);
}
