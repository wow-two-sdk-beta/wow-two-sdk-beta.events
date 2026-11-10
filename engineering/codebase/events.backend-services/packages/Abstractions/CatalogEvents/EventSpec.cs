namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Describes a curator's write: the facts, and the revision read before changing an existing event.</summary>
public sealed record EventSpec
{
    /// <summary>Gets the facts to write.</summary>
    public required EventFacts Facts { get; init; }

    /// <summary>Gets the revision the caller read; required to change an existing event.</summary>
    public int? ExpectedRevision { get; init; }
}
