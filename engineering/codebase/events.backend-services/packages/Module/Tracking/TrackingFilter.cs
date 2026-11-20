using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Names a facet whose own selection a count leaves out.</summary>
[Flags]
internal enum FacetOmit
{
    None = 0,
    Countries = 1,
    Cities = 2,
    Kinds = 4,
    Stages = 8,
    Priority = 16,
    Sources = 32,
    Deadline = 64,
}

/// <summary>Holds a <see cref="TrackingQuery"/>'s filters, checked and normalized, and builds the query over a catalog's
/// published events and the organization's tracking that the list and its facets share.</summary>
/// <param name="Events">The filters the events list shares.</param>
/// <param name="TrackedOnly">Whether only tracked events list.</param>
/// <param name="Stages">The stages to list; lowercase not applied, keys as given.</param>
/// <param name="ExcludeStages">The stages to leave out.</param>
/// <param name="Priority">The priority to list; implies tracked events only.</param>
/// <param name="SourceNames">The source names to list, lowercase, without the reserved <c>curated</c>.</param>
/// <param name="Curated">Whether the events no intake source reported list.</param>
/// <param name="PublishedSince">The first instant an event may have been published at.</param>
/// <param name="Text">The lowercased text to match.</param>
/// <param name="Sort">How the list is ordered.</param>
internal sealed record TrackingFilter(
    EventListFilter Events,
    bool TrackedOnly,
    string[] Stages,
    string[] ExcludeStages,
    TrackingPriority? Priority,
    string[] SourceNames,
    bool Curated,
    DateTimeOffset? PublishedSince,
    string? Text,
    TrackingSort Sort)
{
    /// <summary>Gets whether the stage selections need the organization's workflow.</summary>
    public bool NeedsWorkflow => Stages.Length > 0 || ExcludeStages.Length > 0;

    /// <summary>Checks a query and normalizes its filters.</summary>
    /// <param name="query">The query.</param>
    /// <param name="filter">The filters; unset when the query is refused.</param>
    /// <returns>The validation error; none when the query reads.</returns>
    public static AppError? TryCreate(TrackingQuery query, out TrackingFilter filter)
    {
        filter = null!;
        if ((EventListQuery.CheckCountries("countries", query.Countries) ?? EventListQuery.CheckCountries("excludeCountries", query.ExcludeCountries)) is { } invalid)
            return invalid;

        var stages = StageKeys(query.Stages, query.Stage);
        var excluded = StageKeys(query.ExcludeStages, null);
        if ((CheckStages("stages", stages) ?? CheckStages("excludeStages", excluded)) is { } badStages)
            return badStages;
        if (stages.Intersect(excluded, StringComparer.Ordinal).Any())
            return EventsErrors.Invalid("excludeStages", "a stage may not be both listed and left out");
        if (!Enum.IsDefined(query.Sort))
            return EventsErrors.Invalid("sort", "starts, added or deadline");

        if ((CheckList("kinds", query.Kinds, 64) ?? CheckList("cities", query.Cities, EventsConstants.MaxNameLength) ?? CheckList("sources", query.Sources, EventsConstants.MaxNameLength)) is { } badList)
            return badList;

        var sources = EventListQuery.Normalized(query.Sources, null);
        var events = new EventListFilter(
            query.From, query.To, EventListQuery.Normalize(query.Kind), query.Country, EventListQuery.Normalize(query.City),
            query.Status is { } status ? EventsLabels.Of(status) : null, query.DeadlineBefore, query.IncludeUndated,
            query.Countries, query.ExcludeCountries, query.IncludeUnknownCountry, query.Kinds, query.Cities);
        filter = new TrackingFilter(
            events, query.TrackedOnly, stages, excluded, query.Priority,
            sources.Where(source => source != EventsConstants.CuratedSource).ToArray(),
            sources.Contains(EventsConstants.CuratedSource),
            query.PublishedSince, EventListQuery.Normalize(query.Text), query.Sort);
        return null;
    }

    /// <summary>Builds the organization's published events of a catalog with its tracking, under every selection of the
    /// query except the facets named in <paramref name="omit"/>.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="catalogId">The catalog.</param>
    /// <param name="caller">The caller, whose organization's tracking joins.</param>
    /// <param name="defaultStage">The workflow's default stage, which an untracked event reads as.</param>
    /// <param name="today">The day an open deadline must still be open on (UTC).</param>
    /// <param name="omit">The facets whose selection is left out.</param>
    public IQueryable<TrackedRow> Rows(EventsDbContext db, Guid catalogId, EventsCaller caller, string defaultStage, DateOnly today, FacetOmit omit = FacetOmit.None)
    {
        var filter = Events;
        if (omit.HasFlag(FacetOmit.Countries))
            filter = filter with { Country = null, Countries = [], ExcludeCountries = [], IncludeUnknownCountry = false };
        if (omit.HasFlag(FacetOmit.Cities))
            filter = filter with { City = null, Cities = [] };
        if (omit.HasFlag(FacetOmit.Kinds))
            filter = filter with { Kind = null, Kinds = [] };
        if (omit.HasFlag(FacetOmit.Deadline))
            filter = filter with { DeadlineBefore = null };

        var events = EventListQuery.Filter(EventListQuery.PublishedEvents(db, catalogId), db, filter, today);
        if (PublishedSince is { } since)
            events = events.Where(row => row.PublishedAt >= since);
        if (!omit.HasFlag(FacetOmit.Sources))
            events = BySource(events, db);

        var issuer = caller.Issuer;
        var organization = caller.OrganizationId!;
        var joined =
            from row in events
            join tracking in db.Tracking.AsNoTracking().Where(item => item.Issuer == issuer && item.OrganizationId == organization)
                on row.Id equals tracking.EventId into tracked
            from tracking in tracked.DefaultIfEmpty()
            select new TrackedRow { Event = row, Tracking = tracking };

        var priority = omit.HasFlag(FacetOmit.Priority) ? null : Priority;
        if (TrackedOnly || priority is not null)
            joined = joined.Where(item => item.Tracking != null);

        // Stages read against the organization's workflow: an untracked event stands in its default stage (PG02), as a
        // digest's stage selection reads it.
        if (!omit.HasFlag(FacetOmit.Stages))
        {
            var stages = Stages;
            var excluded = ExcludeStages;
            if (stages.Length > 0)
                joined = stages.Contains(defaultStage, StringComparer.Ordinal)
                    ? joined.Where(item => item.Tracking == null || stages.Contains(item.Tracking.Stage))
                    : joined.Where(item => item.Tracking != null && stages.Contains(item.Tracking.Stage));
            if (excluded.Length > 0)
                joined = excluded.Contains(defaultStage, StringComparer.Ordinal)
                    ? joined.Where(item => item.Tracking != null && !excluded.Contains(item.Tracking.Stage))
                    : joined.Where(item => item.Tracking == null || !excluded.Contains(item.Tracking.Stage));
        }

        if (priority is { } listed)
        {
            var label = EventsLabels.Of(listed);
            joined = joined.Where(item => item.Tracking!.Priority == label);
        }

        if (Text is { } text)
        {
            // A two-letter search also finds the events in that country (PG18): "UZ" or "kz".
            joined = EventListQuery.CountryCode(text) is { } code
                ? joined.Where(item => item.Event.Search.Contains(text) || item.Event.Country == code
                    || (item.Tracking != null && item.Tracking.Notes.ToLower().Contains(text)))
                : joined.Where(item => item.Event.Search.Contains(text) || (item.Tracking != null && item.Tracking.Notes.ToLower().Contains(text)));
        }

        return joined;
    }

    /// <summary>Keeps the events one of the listed sources reported: by the name the source is stored under, and the
    /// reserved <c>curated</c> for the events no intake source reported.</summary>
    private IQueryable<CatalogEventEntity> BySource(IQueryable<CatalogEventEntity> events, EventsDbContext db)
    {
        var names = SourceNames;
        var curated = Curated;
        var intake = EventsLabels.Of(EventSourceKind.Scfd);
        return (names.Length > 0, curated) switch
        {
            (false, false) => events,
            (false, true) => events.Where(row => !db.Sources.Any(source => source.EventId == row.Id && source.Kind == intake)),
            (true, false) => events.Where(row => db.Sources.Any(source => source.EventId == row.Id && source.Name != null && names.Contains(source.Name.ToLower()))),
            (true, true) => events.Where(row =>
                !db.Sources.Any(source => source.EventId == row.Id && source.Kind == intake)
                || db.Sources.Any(source => source.EventId == row.Id && source.Name != null && names.Contains(source.Name.ToLower()))),
        };
    }

    /// <summary>Gathers stage keys: the list and the single key, trimmed and distinct.</summary>
    private static string[] StageKeys(IReadOnlyList<string>? keys, string? single) =>
        (keys ?? []).Append(single).Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key!.Trim()).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>Checks stage keys: at most <see cref="EventsConstants.MaxListLength"/>, each of the key pattern.</summary>
    private static AppError? CheckStages(string field, string[] keys) =>
        keys.Length > EventsConstants.MaxListLength ? EventsErrors.Invalid(field, $"at most {EventsConstants.MaxListLength} stages")
        : keys.Any(key => key.Length > 64 || !EventFactsValidator.KeyPattern().IsMatch(key)) ? EventsErrors.Invalid(field, "stage keys")
        : null;

    /// <summary>Checks a list of values: at most <see cref="EventsConstants.MaxListLength"/>, each no longer than
    /// <paramref name="longest"/>.</summary>
    private static AppError? CheckList(string field, IReadOnlyList<string>? values, int longest)
    {
        if (values is null || values.Count == 0)
            return null;
        if (values.Count > EventsConstants.MaxListLength)
            return EventsErrors.Invalid(field, $"at most {EventsConstants.MaxListLength} values");
        return values.Any(value => value is null || value.Trim().Length > longest)
            ? EventsErrors.Invalid(field, $"values of at most {longest} characters")
            : null;
    }
}
