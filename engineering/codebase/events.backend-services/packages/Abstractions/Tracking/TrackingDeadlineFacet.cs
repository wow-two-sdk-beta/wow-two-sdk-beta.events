namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents how many events have an open deadline inside a window ending on one day.</summary>
/// <param name="Before">The last day an open deadline may fall on: the query's <see cref="TrackingQuery.DeadlineBefore"/>,
/// else <see cref="EventsConstants.DeadlineWindowDays"/> days after today (UTC).</param>
/// <param name="Count">The events that match under the query's other selections.</param>
public sealed record TrackingDeadlineFacet(DateOnly Before, int Count);
