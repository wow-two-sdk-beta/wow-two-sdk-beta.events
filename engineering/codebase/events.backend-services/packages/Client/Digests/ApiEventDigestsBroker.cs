using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Digests;

/// <summary>Integrates the running Events service's digests of the caller's active organization.</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiEventDigestsBroker(EventsApiTransport transport) : IEventDigestsBroker
{
    /// <summary>Holds a digest's preview segment.</summary>
    private const string Preview = "preview";

    /// <summary>Holds a digest's send now segment.</summary>
    private const string Send = "send";

    /// <summary>Holds a digest's runs segment.</summary>
    private const string Runs = "runs";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<DigestModel>>> ListAsync(string catalog, CancellationToken cancellationToken = default) =>
        transport.SendAsync<IReadOnlyList<DigestModel>>(HttpMethod.Get, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Digests), cancellationToken);

    /// <inheritdoc />
    public Task<Result<DigestModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default) =>
        transport.SendAsync<DigestModel>(HttpMethod.Get, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Digests, id), cancellationToken);

    /// <inheritdoc />
    public Task<Result<DigestModel>> CreateAsync(string catalog, DigestSpec spec, CancellationToken cancellationToken = default) =>
        transport.SendAsync<DigestSpec, DigestModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Digests), spec, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DigestModel>> UpdateAsync(string catalog, Guid id, DigestSpec spec, CancellationToken cancellationToken = default) =>
        transport.SendAsync<DigestSpec, DigestModel>(HttpMethod.Put, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Digests, id), spec, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DigestModel>> DeleteAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) =>
        transport.SendAsync<DigestModel>(
            HttpMethod.Delete,
            new EventsApiQuery().Add("expectedRevision", expectedRevision).ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Digests, id)),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<DigestPreviewModel>> PreviewAsync(string catalog, Guid id, CancellationToken cancellationToken = default) =>
        transport.SendAsync<DigestPreviewModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Digests, id, Preview), cancellationToken);

    /// <inheritdoc />
    public Task<Result<DigestRunModel>> SendNowAsync(string catalog, Guid id, CancellationToken cancellationToken = default) =>
        transport.SendAsync<DigestRunModel>(HttpMethod.Post, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Digests, id, Send), cancellationToken);

    /// <inheritdoc />
    public Task<Result<DigestRunPage>> ListRunsAsync(string catalog, Guid id, DigestRunQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var route = new EventsApiQuery()
            .Add("cursor", query.Cursor)
            .Add("limit", query.Limit)
            .ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Digests, id, Runs));
        return transport.SendAsync<DigestRunPage>(HttpMethod.Get, route, cancellationToken);
    }
}
