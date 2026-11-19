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

    /// <summary>Gets the tracking's stage; the workflow's default stage for an event the organization does not track.</summary>
    public required string Stage { get; init; }

    /// <summary>Gets whether the organization tracks the event; false when <see cref="Stage"/> is the default stage it stands
    /// in untracked. True when absent, as a service from before untracked deadlines answers.</summary>
    public bool Tracked { get; init; } = true;
}
