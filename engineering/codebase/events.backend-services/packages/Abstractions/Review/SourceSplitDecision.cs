using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Describes a split: the source identity that leaves an event for a candidate of its own, and the revision read.</summary>
public sealed record SourceSplitDecision
{
    /// <summary>Gets how the source reported the event (<see cref="EventSourceModel.Kind"/>).</summary>
    public required EventSourceKind Kind { get; init; }

    /// <summary>Gets the source's identity within its kind (<see cref="EventSourceModel.Id"/>).</summary>
    public required string SourceId { get; init; }

    /// <summary>Gets the event's revision the reviewer read.</summary>
    public required int ExpectedRevision { get; init; }
}
