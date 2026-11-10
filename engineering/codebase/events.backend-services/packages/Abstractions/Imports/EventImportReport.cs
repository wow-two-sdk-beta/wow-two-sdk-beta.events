namespace WoW.Two.Sdk.Beta.Be.Events.Imports;

/// <summary>Represents what an import did, or would do in a dry run.</summary>
public sealed record EventImportReport
{
    /// <summary>Gets whether nothing was written.</summary>
    public required bool DryRun { get; init; }

    /// <summary>Gets the bundle's source.</summary>
    public required string Source { get; init; }

    /// <summary>Gets how many rows created events.</summary>
    public required int Created { get; init; }

    /// <summary>Gets how many rows changed events or tracking.</summary>
    public required int Updated { get; init; }

    /// <summary>Gets how many rows changed nothing.</summary>
    public required int Unchanged { get; init; }

    /// <summary>Gets how many rows failed.</summary>
    public required int Failed { get; init; }

    /// <summary>Gets the stable old → new id map, one entry per row, in the bundle's order.</summary>
    public required IReadOnlyList<EventImportMapEntry> Map { get; init; }
}
