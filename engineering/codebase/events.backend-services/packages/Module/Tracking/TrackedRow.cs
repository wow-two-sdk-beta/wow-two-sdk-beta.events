using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Holds a published event with the caller organization's tracking of it, as the tracking list and its facets
/// read them.</summary>
internal sealed class TrackedRow
{
    /// <summary>Gets the event.</summary>
    public required CatalogEventEntity Event { get; init; }

    /// <summary>Gets the organization's tracking; none when it does not track the event.</summary>
    public TrackingEntity? Tracking { get; init; }

    /// <summary>Gets the earliest start of the event's open deadlines; read by the deadline sort only.</summary>
    public DateOnly? Next { get; init; }
}
