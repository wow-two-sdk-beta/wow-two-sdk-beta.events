using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

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
        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            var country = filter.Country.Trim().ToUpperInvariant();
            events = events.Where(row => row.Country == country);
        }

        if (Normalize(filter.City) is { } city)
            events = events.Where(row => row.City == city);
        if (filter.Status is { } status)
            events = events.Where(row => row.Status == status);
        if (filter.DeadlineBefore is { } before)
            events = events.Where(row => db.Deadlines.Any(deadline =>
                deadline.EventId == row.Id && deadline.AtMin <= before && deadline.AtMax >= today));

        return events;
    }

    /// <summary>Orders events as the events list does: start ascending with unknown last, then title, then id.</summary>
    public static IOrderedQueryable<CatalogEventEntity> Ordered(IQueryable<CatalogEventEntity> events) =>
        events.OrderBy(row => row.StartsMin == null).ThenBy(row => row.StartsMin).ThenBy(row => row.TitleSort).ThenBy(row => row.Id);

    /// <summary>Gets a text filter as the lowercased needle the <c>search</c> column is matched with; none when blank.</summary>
    public static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLowerInvariant();

    /// <summary>Gets the day in UTC.</summary>
    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
