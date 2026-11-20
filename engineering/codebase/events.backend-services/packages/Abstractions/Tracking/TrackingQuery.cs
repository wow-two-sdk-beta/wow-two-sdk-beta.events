using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Filters, orders and pages a catalog's published events joined with the caller organization's tracking.</summary>
/// <remarks>The event filters work as in <c>EventQuery</c>; the order is <see cref="Sort"/>. A list filter matches an event
/// that is in any of its values, and the single value beside it (<see cref="Kind"/>, <see cref="City"/>) adds to the
/// list; different filters all have to match.</remarks>
public sealed record TrackingQuery
{
    /// <summary>Gets whether only tracked events are listed.</summary>
    public bool TrackedOnly { get; init; }

    /// <summary>Gets the stage to list; with <see cref="Stages"/>, an event in either matches. An untracked event reads as
    /// the organization's workflow's default stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the stages to list (an event in any matches, with <see cref="Stage"/>); an untracked event reads as the
    /// workflow's default stage, so it lists only when the default stage is named. At most
    /// <see cref="EventsConstants.MaxListLength"/>.</summary>
    public IReadOnlyList<string> Stages { get; init; } = [];

    /// <summary>Gets the stages to leave out; an untracked event reads as the workflow's default stage, so it is left out
    /// only when the default stage is named. At most <see cref="EventsConstants.MaxListLength"/>; a key may not be in both
    /// lists.</summary>
    public IReadOnlyList<string> ExcludeStages { get; init; } = [];

    /// <summary>Gets the priority to list; implies tracked events only.</summary>
    public TrackingPriority? Priority { get; init; }

    /// <summary>Gets the first day to list.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Gets the last day to list.</summary>
    public DateOnly? To { get; init; }

    /// <summary>Gets the kind to list; with <see cref="Kinds"/>, an event of either matches.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets the kinds to list: an event matches when its kind is one of them (and <see cref="Kind"/>), at most
    /// <see cref="EventsConstants.MaxListLength"/>.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];

    /// <summary>Gets the city to list, matched without case; with <see cref="Cities"/>, an event in either matches.</summary>
    public string? City { get; init; }

    /// <summary>Gets the cities to list, matched without case: an event matches when its city is one of them (and
    /// <see cref="City"/>), at most <see cref="EventsConstants.MaxListLength"/>.</summary>
    public IReadOnlyList<string> Cities { get; init; } = [];

    /// <summary>Gets the status to list.</summary>
    public EventStatus? Status { get; init; }

    /// <summary>Gets the source names to list, matched without case against the name each source of an event is stored
    /// under (<c>Afisha</c>): an event matches when any of its sources is one of them. The reserved value
    /// <see cref="EventsConstants.CuratedSource"/> matches the events no intake source reported — imports and curators'
    /// own entries. At most <see cref="EventsConstants.MaxListLength"/>.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>Gets the first instant an event may have been published at: lists the events published on or after it.</summary>
    public DateTimeOffset? PublishedSince { get; init; }

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

    /// <summary>Gets how the list is ordered; <see cref="TrackingSort.Starts"/> when not set.</summary>
    public TrackingSort Sort { get; init; } = TrackingSort.Starts;

    /// <summary>Gets the cursor a previous page returned; it holds for the sort it came from, and a list under another sort
    /// refuses it with <see cref="EventsErrors.CursorInvalid"/>.</summary>
    public string? Cursor { get; init; }

    /// <summary>Gets the page size, at most <see cref="EventsConstants.MaxPageSize"/>.</summary>
    public int Limit { get; init; } = EventsConstants.DefaultPageSize;
}
