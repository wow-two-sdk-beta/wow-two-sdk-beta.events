using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Status.Models;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Status.Controllers;

/// <summary>Exposes system status over HTTP.</summary>
[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    /// <summary>Holds the name the service reports.</summary>
    private const string ServiceName = "events";

    /// <summary>Reports service liveness and the SCFD intake's health (the services sweep: the dev check could not see a
    /// dead intake).</summary>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    /// <returns>The service name, liveness state, running version and the intake's health.</returns>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<SystemStatusDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var version = typeof(SystemController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var status = new SystemStatusDto { Service = ServiceName, Status = "ok", Version = version, Intake = await IntakeAsync(cancellationToken) };

        return Ok(ApiResponse<SystemStatusDto>.Ok(status));
    }

    /// <summary>Reads the intake's health; none when no intake is registered or its rows cannot be read.</summary>
    private async Task<IntakeHealth?> IntakeAsync(CancellationToken cancellationToken)
    {
        if (HttpContext.RequestServices.GetService<IEventsIntakeHealth>() is not { } health)
            return null;

        try
        {
            return await health.ReadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }
}
