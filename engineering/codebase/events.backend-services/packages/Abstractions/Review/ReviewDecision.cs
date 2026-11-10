using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Describes a reviewer's decision on an event or a change: the revision read and, optionally, edited facts.</summary>
public sealed record ReviewDecision
{
    /// <summary>Gets the event's revision the reviewer read.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the facts to publish instead of the proposed ones; none keeps them as proposed.</summary>
    public EventFacts? Facts { get; init; }
}
