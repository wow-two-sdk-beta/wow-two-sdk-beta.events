namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents one stage change of a tracking record.</summary>
public sealed record TrackingHistoryEntryModel
{
    /// <summary>Gets the stage before; none when the tracking started.</summary>
    public string? FromStage { get; init; }

    /// <summary>Gets the stage after.</summary>
    public required string ToStage { get; init; }

    /// <summary>Gets the actor's issuer.</summary>
    public string? ActorIssuer { get; init; }

    /// <summary>Gets the actor's subject.</summary>
    public string? ActorSubject { get; init; }

    /// <summary>Gets when it changed (UTC).</summary>
    public required DateTimeOffset AtUtc { get; init; }
}
