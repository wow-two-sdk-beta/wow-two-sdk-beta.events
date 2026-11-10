namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents one page of events.</summary>
/// <param name="Items">The events on this page.</param>
/// <param name="NextCursor">The cursor of the next page; none on the last page.</param>
public sealed record EventPage(IReadOnlyList<EventModel> Items, string? NextCursor);
