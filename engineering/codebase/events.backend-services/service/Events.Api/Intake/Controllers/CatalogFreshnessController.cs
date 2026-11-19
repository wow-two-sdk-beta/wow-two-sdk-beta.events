using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Intake.Controllers;

/// <summary>Exposes a catalog's intake freshness through <see cref="IEventIntakeBroker"/> to the catalog's members:
/// <c>events.read</c>, and a person's <c>events:read</c>. The status and the poll stay with
/// <see cref="IntakeController"/> and <c>events.admin</c>.</summary>
/// <param name="intake">The intake broker.</param>
[Route(EventsRouteConstants.Catalogs + "/{catalog}/" + EventsRouteConstants.Freshness)]
[Authorize(Policy = EventsScopeConstants.Read)]
public sealed class CatalogFreshnessController(IEventIntakeBroker intake) : EventsControllerBase
{
    /// <summary>Reports when the catalog's sources last received records, the newest observed time and how many sources
    /// fail; a catalog of another organization answers 404.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The freshness.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IntakeFreshnessModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Freshness(string catalog, CancellationToken ct) => Answer(await intake.GetFreshnessAsync(catalog, ct));
}
