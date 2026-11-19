namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents one source identity that reported an event; an event keeps every one.</summary>
public sealed record EventSourceModel
{
    /// <summary>Gets how the source reported it.</summary>
    public required EventSourceKind Kind { get; init; }

    /// <summary>Gets the identity within the kind: <c>{subscription}/{post}</c> for SCFD, <c>{source}/{legacyId}</c> for an
    /// import, the curator's subject for a manual entry.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the page the event was found on.</summary>
    public string? Url { get; init; }

    /// <summary>Gets the source's name as people know it, such as <c>Afisha</c>: for SCFD, the subscription's source name,
    /// else its source group, else the intake source's own name; none for an import or a curator's entry.</summary>
    public string? Name { get; init; }

    /// <summary>Gets where the source's listing stands, as the source last reported it; none when the source reports no
    /// state (an import, a curator's entry, or an SCFD record from before Events kept it).</summary>
    public EventSourceState? State { get; init; }

    /// <summary>Gets when the source posted the listing (UTC); none when the source does not say.</summary>
    public DateTimeOffset? PostedAtUtc { get; init; }

    /// <summary>Gets the rule that attached the source to this event after another source started it, such as
    /// <c>rule:title-date-city</c>; none for the source the event started from, an import's and a curator's.</summary>
    public string? MatchedBy { get; init; }

    /// <summary>Gets when the source first reported it (UTC).</summary>
    public required DateTimeOffset FirstSeenAtUtc { get; init; }

    /// <summary>Gets when the source last reported it (UTC).</summary>
    public required DateTimeOffset LastSeenAtUtc { get; init; }
}
