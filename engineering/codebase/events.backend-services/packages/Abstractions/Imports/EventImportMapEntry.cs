namespace WoW.Two.Sdk.Beta.Be.Events.Imports;

/// <summary>Represents one row's old id and the ids Events holds it under.</summary>
public sealed record EventImportMapEntry
{
    /// <summary>Gets the row's id in the producing system.</summary>
    public required string LegacyId { get; init; }

    /// <summary>Gets the event's id; a dry run returns the id a real run would create.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the tracking's id, when the row carries tracking.</summary>
    public Guid? TrackingId { get; init; }

    /// <summary>Gets what the import did with the row.</summary>
    public required EventImportOutcome Outcome { get; init; }

    /// <summary>Gets why the row failed.</summary>
    public string? Error { get; init; }
}
