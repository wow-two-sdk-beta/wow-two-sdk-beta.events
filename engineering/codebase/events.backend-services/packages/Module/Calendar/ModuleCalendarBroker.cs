using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Calendar;

/// <summary>Integrates calendar export in process: a catalog's published events matching a query, every page, as one
/// iCalendar file (<see cref="CalendarWriter"/>).</summary>
/// <remarks>Reads as a catalog read: <see cref="EventsErrors.CatalogNotFound"/> when the caller cannot see the catalog.
/// The query's review state, cursor and limit are ignored; the filters and the order are the events list's.</remarks>
/// <param name="db">The module's store.</param>
/// <param name="callers">Who calls.</param>
/// <param name="time">The clock an open deadline is judged by.</param>
internal sealed class ModuleCalendarBroker(EventsDbContext db, IEventsCallerService callers, TimeProvider time) : IEventCalendarBroker
{
    /// <inheritdoc />
    public async Task<Result<CalendarFile>> ExportAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default)
    {
        var found = await CatalogAccess.FindReadableAsync(db, callers.Current, catalog, cancellationToken);
        if (found is not Result<CatalogEntity>.Success { Value: var catalogRow })
            return Result<CalendarFile>.Fail(((Result<CatalogEntity>.Failure)found).Error);

        query ??= new EventQuery();
        var filter = new EventListFilter(
            query.From, query.To, query.Kind, query.Country, query.City,
            query.Status is { } status ? EventsLabels.Of(status) : null,
            query.DeadlineBefore, query.IncludeUndated);
        var events = EventListQuery.Filter(EventListQuery.PublishedEvents(db, catalogRow.Id), db, filter, EventListQuery.Today(time));
        if (EventListQuery.Normalize(query.Text) is { } text)
            events = events.Where(row => row.Search.Contains(text));

        var rows = await EventListQuery.Ordered(events).ToListAsync(cancellationToken);
        var models = rows.Select(row => EventRows.ToModel(row, catalogRow.Slug, [], 0));
        return Result<CalendarFile>.Ok(new CalendarFile($"{catalogRow.Slug}.ics", CalendarWriter.Write(catalogRow.Name, models)));
    }
}
