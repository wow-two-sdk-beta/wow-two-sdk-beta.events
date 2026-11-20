namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents the counts behind a tracking list's filter chips, for one query.</summary>
/// <remarks>Each facet is counted without its own selection — <see cref="TrackingFacetsModel.Cities"/> ignores the query's
/// <c>City</c> and <c>Cities</c> and honours every other filter — so a chip shows what picking it would list.
/// <see cref="Total"/> counts the whole query.</remarks>
public sealed record TrackingFacetsModel
{
    /// <summary>Gets how many events the whole query lists, whatever the page.</summary>
    public required int Total { get; init; }

    /// <summary>Gets the events per ISO-2 country, most first; events with no country count nowhere. Ignores the country
    /// selections (<c>Country</c>, <c>Countries</c>, <c>ExcludeCountries</c>, <c>IncludeUnknownCountry</c>).</summary>
    public required IReadOnlyList<TrackingFacetCount> Countries { get; init; }

    /// <summary>Gets the events per city, lowercase as stored, most first; events with no city count nowhere. Ignores
    /// <c>City</c> and <c>Cities</c>.</summary>
    public required IReadOnlyList<TrackingFacetCount> Cities { get; init; }

    /// <summary>Gets the events per kind: the catalog's kinds in its order, zeros included, then any other kind found.
    /// Ignores <c>Kind</c> and <c>Kinds</c>.</summary>
    public required IReadOnlyList<TrackingFacetCount> Kinds { get; init; }

    /// <summary>Gets the events per stage of the organization's workflow, in its order, zeros included; an untracked event
    /// counts under the default stage. Ignores <c>Stage</c>, <c>Stages</c> and <c>ExcludeStages</c>.</summary>
    public required IReadOnlyList<TrackingFacetCount> Stages { get; init; }

    /// <summary>Gets the tracked events per priority (<c>high</c>, <c>normal</c>, <c>low</c>), zeros included; an untracked
    /// event has none. Ignores <c>Priority</c>.</summary>
    public required IReadOnlyList<TrackingFacetCount> Priorities { get; init; }

    /// <summary>Gets the events per source name, most first, each with its name as stored as the label;
    /// <see cref="EventsConstants.CuratedSource"/> counts the events no intake source reported. An event with several
    /// sources counts once under each name. Ignores <c>Sources</c>.</summary>
    public required IReadOnlyList<TrackingFacetCount> Sources { get; init; }

    /// <summary>Gets the events with an open deadline inside the window. Ignores <c>DeadlineBefore</c>, which only sets the
    /// window's end.</summary>
    public required TrackingDeadlineFacet OpenDeadline { get; init; }
}
