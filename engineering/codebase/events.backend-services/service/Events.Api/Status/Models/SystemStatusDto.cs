using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Api.Status.Models;

/// <summary>Represents the liveness and identity of the service.</summary>
public sealed record SystemStatusDto
{
    /// <summary>Gets the service name.</summary>
    public required string Service { get; init; }

    /// <summary>Gets the liveness state.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the informational version of the running build, with its commit.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the SCFD intake's health: <c>ok</c>, <c>failing</c> when a source's last poll failed, or <c>off</c>,
    /// with each active source's state; the dev check refuses a failing intake. None when it could not be read.</summary>
    public IntakeHealth? Intake { get; init; }
}
