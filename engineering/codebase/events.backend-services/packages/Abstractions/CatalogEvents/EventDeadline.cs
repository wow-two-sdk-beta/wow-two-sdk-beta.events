namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents a date before an event by which something must be done.</summary>
public sealed record EventDeadline
{
    /// <summary>Gets what the deadline closes.</summary>
    public required DeadlineKind Kind { get; init; }

    /// <summary>Gets when it closes.</summary>
    public required EventWhen At { get; init; }

    /// <summary>Gets a label.</summary>
    public string? Label { get; init; }

    /// <summary>Gets where to act.</summary>
    public string? Url { get; init; }
}
