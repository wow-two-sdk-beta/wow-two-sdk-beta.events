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

    /// <summary>Gets when the source first reported it (UTC).</summary>
    public required DateTimeOffset FirstSeenAtUtc { get; init; }

    /// <summary>Gets when the source last reported it (UTC).</summary>
    public required DateTimeOffset LastSeenAtUtc { get; init; }
}
