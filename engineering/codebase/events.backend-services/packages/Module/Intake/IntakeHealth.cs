namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Represents the intake's health for the host's status: whether every active source's last poll worked.</summary>
/// <param name="Status"><c>ok</c>, <c>failing</c> when any source's last poll failed, or <c>off</c> with no active source.</param>
/// <param name="Sources">Each active source's health.</param>
public sealed record IntakeHealth(string Status, IReadOnlyList<IntakeSourceHealth> Sources)
{
    /// <summary>Holds the status of an intake whose sources work.</summary>
    public const string Ok = "ok";

    /// <summary>Holds the status of an intake with a source whose last poll failed.</summary>
    public const string Failing = "failing";

    /// <summary>Holds the status of an intake with no active source.</summary>
    public const string Off = "off";

    /// <summary>Holds the status of a source that has not polled yet.</summary>
    public const string Waiting = "waiting";
}

/// <summary>Represents one active source's health: names, counts and times only, never an error text, a host or a token.</summary>
/// <param name="Name">The source's name.</param>
/// <param name="Status"><c>ok</c>, <c>failing</c> or <c>waiting</c> before its first poll.</param>
/// <param name="ConsecutiveFailures">How many polls in a row failed.</param>
/// <param name="LastPolledAtUtc">When it last polled.</param>
public sealed record IntakeSourceHealth(string Name, string Status, int ConsecutiveFailures, DateTimeOffset? LastPolledAtUtc);

/// <summary>Reads the intake's health for the host's status endpoint, with no caller: every active source's last poll.</summary>
public interface IEventsIntakeHealth
{
    /// <summary>Reads the health of every active source.</summary>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The intake's health.</returns>
    Task<IntakeHealth> ReadAsync(CancellationToken cancellationToken = default);
}
