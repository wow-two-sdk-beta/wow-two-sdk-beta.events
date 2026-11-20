namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents how many events one facet value matches.</summary>
/// <param name="Value">The value as the list's filter takes it: an ISO-2 country, a lowercase city, a kind or stage key, a
/// priority's wire name, a lowercase source name or <see cref="EventsConstants.CuratedSource"/>.</param>
/// <param name="Count">The events that match it under the query's other selections.</param>
/// <param name="Label">The value as people read it when no catalog or workflow names it: a source's name as stored,
/// such as <c>Afisha</c>; none for every other facet.</param>
public sealed record TrackingFacetCount(string Value, int Count, string? Label = null);
