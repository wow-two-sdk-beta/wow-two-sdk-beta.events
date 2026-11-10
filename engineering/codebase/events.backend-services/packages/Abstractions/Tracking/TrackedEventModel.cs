using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents an event joined with the caller organization's tracking of it.</summary>
public sealed record TrackedEventModel
{
    /// <summary>Gets the event.</summary>
    public required EventModel Event { get; init; }

    /// <summary>Gets the organization's tracking; none when it does not track the event.</summary>
    public TrackingModel? Tracking { get; init; }
}
