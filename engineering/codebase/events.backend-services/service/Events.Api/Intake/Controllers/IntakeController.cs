using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Intake.Controllers;

/// <summary>Exposes <see cref="IEventIntakeBroker"/> to operators: <c>events.admin</c>, and a person's <c>events:admin</c>.</summary>
/// <param name="intake">The intake broker.</param>
[Route(EventsRouteConstants.Intake)]
[Authorize(Policy = EventsScopeConstants.Admin)]
public sealed class IntakeController(IEventIntakeBroker intake) : EventsControllerBase
{
    /// <summary>Reports each source's cursor, counts and last error.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The status.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IntakeStatusModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct) => Answer(await intake.GetStatusAsync(ct));

    /// <summary>Polls every source once now.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The status after the poll.</returns>
    [HttpPost("poll")]
    [ProducesResponseType<ApiResponse<IntakeStatusModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Poll(CancellationToken ct) => Answer(await intake.PollAsync(ct));
}
