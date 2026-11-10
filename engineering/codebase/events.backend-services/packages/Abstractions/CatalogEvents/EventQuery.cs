namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Filters and pages a catalog's events; every filter is optional.</summary>
/// <remarks>A month or year value covers its whole span; events with no date match only with <see cref="IncludeUndated"/>.
/// Order: start ascending, unknown last, then title, then id.</remarks>
public sealed record EventQuery
{
    /// <summary>Gets the first day to list: an event matches when it has not ended before it.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Gets the last day to list: an event matches when it starts on or before it.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Gets the kind to list.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets the ISO-2 country to list.</summary>
    public string? Country { get; init; }

    /// <summary>Gets the city to list, matched without case.</summary>
    public string? City { get; init; }

    /// <summary>Gets text matched in titles, organizer, city, venue and key.</summary>
    public string? Text { get; init; }

    /// <summary>Gets the status to list.</summary>
    public EventStatus? Status { get; init; }

    /// <summary>Gets the review state to list; reviewers may ask for any, everyone else reads published events only.</summary>
    public EventReviewState? Review { get; init; }

    /// <summary>Gets the last day a deadline may fall on: lists events with an open deadline before it.</summary>
    public DateOnly? DeadlineBefore { get; init; }

    /// <summary>Gets whether events with no known start are listed too.</summary>
    public bool IncludeUndated { get; init; } = true;

    /// <summary>Gets the cursor a previous page returned.</summary>
    public string? Cursor { get; init; }

    /// <summary>Gets the page size, at most <see cref="EventsConstants.MaxPageSize"/>.</summary>
    public int Limit { get; init; } = EventsConstants.DefaultPageSize;
}
