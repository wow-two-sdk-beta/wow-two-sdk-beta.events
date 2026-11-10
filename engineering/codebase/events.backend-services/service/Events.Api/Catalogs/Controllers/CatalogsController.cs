using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Catalogs.Controllers;

/// <summary>Exposes <see cref="IEventCatalogsBroker"/>: reads take <c>events.read</c>, a put <c>events.admin</c>.</summary>
/// <param name="catalogs">The catalogs broker.</param>
[Route(EventsRouteConstants.Catalogs)]
public sealed class CatalogsController(IEventCatalogsBroker catalogs) : EventsControllerBase
{
    /// <summary>Lists the catalogs the caller may read.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The catalogs.</returns>
    [HttpGet]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CatalogModel>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Answer(await catalogs.ListAsync(ct));

    /// <summary>Gets a catalog; one the caller cannot read answers 404.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The catalog.</returns>
    [HttpGet("{catalog}")]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<CatalogModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string catalog, CancellationToken ct) => Answer(await catalogs.GetAsync(catalog, ct));

    /// <summary>Creates a catalog for the caller's organization, or replaces one it owns.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="spec">The catalog's fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The catalog.</returns>
    [HttpPut("{catalog}")]
    [Authorize(Policy = EventsScopeConstants.Admin)]
    [ProducesResponseType<ApiResponse<CatalogModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Put(string catalog, [FromBody] CatalogSpec spec, CancellationToken ct) => Answer(await catalogs.PutAsync(catalog, spec, ct));
}
