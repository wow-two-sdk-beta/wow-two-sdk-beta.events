using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Imports.Controllers;

/// <summary>Exposes <see cref="IEventImportsBroker"/>: <c>events.import</c>, and a person's <c>events:admin</c>.</summary>
/// <param name="imports">The imports broker.</param>
[Route(EventsRouteConstants.Catalogs + "/{catalog}/" + EventsRouteConstants.Imports)]
public sealed class ImportsController(IEventImportsBroker imports) : EventsControllerBase
{
    /// <summary>Imports a <c>wow2.events-import.v1</c> bundle into the catalog and the caller's tracking.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="bundle">The bundle.</param>
    /// <param name="dryRun">Whether to validate and report only, writing nothing.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The report, with one map entry per row.</returns>
    [HttpPost]
    [Authorize(Policy = EventsScopeConstants.Import)]
    [RequestSizeLimit(64 * 1024 * 1024)]
    [ProducesResponseType<ApiResponse<EventImportReport>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(string catalog, [FromBody] EventImportBundle bundle, [FromQuery] bool dryRun, CancellationToken ct) =>
        Answer(await imports.ImportAsync(catalog, bundle, dryRun, ct));
}
