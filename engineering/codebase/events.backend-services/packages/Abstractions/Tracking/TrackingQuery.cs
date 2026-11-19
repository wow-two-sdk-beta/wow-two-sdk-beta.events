namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Filters and pages a catalog's published events joined with the caller organization's tracking.</summary>
/// <remarks>The event filters work as in <c>EventQuery</c>; the order is the same.</remarks>
public sealed record TrackingQuery
{
    /// <summary>Gets whether only tracked events are listed.</summary>
    public bool TrackedOnly { get; init; }

    /// <summary>Gets the stage to list; implies tracked events only.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the priority to list; implies tracked events only.</summary>
    public TrackingPriority? Priority { get; init; }

    /// <summary>Gets the first day to list.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Gets the last day to list.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Gets the kind to list.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets the ISO-2 country to list; with <see cref="Countries"/>, an event in either matches.</summary>
    public string? Country { get; init; }

    /// <summary>Gets the ISO-2 countries to list: an event matches when its country is one of them (and
    /// <see cref="Country"/>), at most <see cref="EventsConstants.MaxCountryFilter"/>.</summary>
    public IReadOnlyList<string> Countries { get; init; } = [];

    /// <summary>Gets the ISO-2 countries to leave out, at most <see cref="EventsConstants.MaxCountryFilter"/>.</summary>
    public IReadOnlyList<string> ExcludeCountries { get; init; } = [];

    /// <summary>Gets whether an event with no known country matches a country filter (<see cref="Country"/>,
    /// <see cref="Countries"/> or <see cref="ExcludeCountries"/>); without a country filter every event matches.</summary>
    public bool IncludeUnknownCountry { get; init; }

    /// <summary>Gets text matched in titles, organizer, city, venue, key and the tracking's notes.</summary>
    public string? Text { get; init; }

    /// <summary>Gets the last day an open deadline may fall on.</summary>
    public DateOnly? DeadlineBefore { get; init; }

    /// <summary>Gets whether events with no known start are listed too.</summary>
    public bool IncludeUndated { get; init; } = true;

    /// <summary>Gets the cursor a previous page returned.</summary>
    public string? Cursor { get; init; }

    /// <summary>Gets the page size, at most <see cref="EventsConstants.MaxPageSize"/>.</summary>
    public int Limit { get; init; } = EventsConstants.DefaultPageSize;
}
