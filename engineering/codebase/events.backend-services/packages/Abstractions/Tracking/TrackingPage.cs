namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents one page of tracked events.</summary>
/// <param name="Items">The events on this page, with the organization's tracking.</param>
/// <param name="NextCursor">The cursor of the next page; none on the last page.</param>
public sealed record TrackingPage(IReadOnlyList<TrackedEventModel> Items, string? NextCursor);
