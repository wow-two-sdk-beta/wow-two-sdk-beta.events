using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents an open deadline of an event the organization tracks.</summary>
public sealed record UpcomingDeadlineModel
{
    /// <summary>Gets the event.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the event's title per locale.</summary>
    public required IReadOnlyDictionary<string, string> Title { get; init; }

    /// <summary>Gets the deadline.</summary>
    public required EventDeadline Deadline { get; init; }

    /// <summary>Gets the tracking's stage.</summary>
    public required string Stage { get; init; }
}
