using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Builds the query over a catalog's events that the tracking list and the calendar export share: the filters
/// and the order of the events list.</summary>
internal static class EventListQuery
{
    /// <summary>Gets the label of a published event.</summary>
    public static string Published { get; } = EventsLabels.Of(EventReviewState.Published);

    /// <summary>Gets the label of a candidate event.</summary>
    public static string Candidate { get; } = EventsLabels.Of(EventReviewState.Candidate);

    /// <summary>Gets a catalog's published events, untracked.</summary>
    public static IQueryable<CatalogEventEntity> PublishedEvents(EventsDbContext db, Guid catalogId)
    {
        var published = Published;
        return db.Events.AsNoTracking().Where(row => row.CatalogId == catalogId && row.Review == published);
    }

    /// <summary>Applies the event filters: dates by the span a value covers, undated events only when asked, and a
    /// deadline that is still open on <paramref name="today"/>.</summary>
    /// <param name="events">The events to filter.</param>
    /// <param name="db">The context, for the deadline rows.</param>
    /// <param name="filter">The filters.</param>
    /// <param name="today">The day a deadline must still be open on (UTC).</param>
    public static IQueryable<CatalogEventEntity> Filter(IQueryable<CatalogEventEntity> events, EventsDbContext db, EventListFilter filter, DateOnly today)
    {
        if (!filter.IncludeUndated)
            events = events.Where(row => row.StartsMin != null);
        if (filter.From is { } from)
            events = events.Where(row => row.StartsMin == null || row.EndsMax >= from);
        if (filter.To is { } to)
            events = events.Where(row => row.StartsMin == null || row.StartsMin <= to);
        if (Normalize(filter.Kind) is { } kind)
            events = events.Where(row => row.Kind == kind);
        // Country lists (E21): an event matches a listed country and no left-out one; one with no country matches only
        // when the filter lets unknown countries in.
        var listed = (filter.Countries ?? []).Append(filter.Country)
            .Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code!.Trim().ToUpperInvariant()).Distinct().ToArray();
        var excluded = (filter.ExcludeCountries ?? []).Select(code => code.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (listed.Length > 0)
            events = filter.IncludeUnknownCountry
                ? events.Where(row => row.Country == null || listed.Contains(row.Country))
                : events.Where(row => row.Country != null && listed.Contains(row.Country));
        if (excluded.Length > 0)
            events = filter.IncludeUnknownCountry
                ? events.Where(row => row.Country == null || !excluded.Contains(row.Country))
                : events.Where(row => row.Country != null && !excluded.Contains(row.Country));

        if (Normalize(filter.City) is { } city)
            events = events.Where(row => row.City == city);
        if (filter.Status is { } status)
            events = events.Where(row => row.Status == status);
        if (filter.DeadlineBefore is { } before)
            events = events.Where(row => db.Deadlines.Any(deadline =>
                deadline.EventId == row.Id && deadline.AtMin <= before && deadline.AtMax >= today));

        return events;
    }

    /// <summary>Reads a search as a country code: two ASCII letters, uppercased; none for any other text.</summary>
    /// <param name="text">The normalized search.</param>
    public static string? CountryCode(string text) =>
        text.Length == 2 && text.All(char.IsAsciiLetter) ? text.ToUpperInvariant() : null;

    /// <summary>Checks a list of ISO-2 country codes: two letters each, at most <see cref="EventsConstants.MaxCountryFilter"/>.</summary>
    /// <param name="field">The field the codes came in.</param>
    /// <param name="codes">The codes.</param>
    /// <returns>The validation error; none when every code reads.</returns>
    public static AppError? CheckCountries(string field, IReadOnlyList<string>? codes)
    {
        if (codes is null || codes.Count == 0)
            return null;
        if (codes.Count > EventsConstants.MaxCountryFilter)
            return EventsErrors.Invalid(field, $"at most {EventsConstants.MaxCountryFilter} countries");
        return codes.All(code => code?.Trim() is { Length: 2 } text && text.All(char.IsAsciiLetter))
            ? null
            : EventsErrors.Invalid(field, "ISO-2 country codes");
    }

    /// <summary>Orders events as the events list does: start ascending with unknown last, then title, then id.</summary>
    public static IOrderedQueryable<CatalogEventEntity> Ordered(IQueryable<CatalogEventEntity> events) =>
        events.OrderBy(row => row.StartsMin == null).ThenBy(row => row.StartsMin).ThenBy(row => row.TitleSort).ThenBy(row => row.Id);

    /// <summary>Gets a text filter as the lowercased needle the <c>search</c> column is matched with; none when blank.</summary>
    public static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLowerInvariant();

    /// <summary>Gets the day in UTC.</summary>
    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(time.StoredUtcNow().UtcDateTime);
}
