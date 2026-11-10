using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Auth;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Workflows.Controllers;

/// <summary>Exposes <see cref="IEventWorkflowsBroker"/> for the caller's active organization: a read takes
/// <c>events.read</c>, a write <c>events.track</c> and a person's <c>events:admin</c>.</summary>
/// <param name="workflows">The workflows broker.</param>
[Route(EventsRouteConstants.Workflow)]
public sealed class WorkflowController(IEventWorkflowsBroker workflows) : EventsControllerBase
{
    /// <summary>Gets the organization's workflow; the first read creates the default.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The workflow.</returns>
    [HttpGet]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<WorkflowModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) => Answer(await workflows.GetAsync(ct));

    /// <summary>Replaces the organization's workflow.</summary>
    /// <param name="spec">The stages, the default stage and the fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The workflow.</returns>
    [HttpPut]
    [Authorize(Policy = EventsAuthConstants.WorkflowWritePolicy)]
    [ProducesResponseType<ApiResponse<WorkflowModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Put([FromBody] WorkflowSpec spec, CancellationToken ct) => Answer(await workflows.PutAsync(spec, ct));
}
