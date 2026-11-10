using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Calendar;

/// <summary>Integrates calendar export of a catalog's published events, in process or over the service.</summary>
/// <remarks>Each event exports with a stable UID (<c>{id}@events</c>) and a SEQUENCE from its revision; a date-precision
/// start exports all-day, a minute start as local time in its zone, month and year precision export no entry; an exact
/// deadline exports as its own all-day entry.</remarks>
public interface IEventCalendarBroker
{
    /// <summary>Exports the events a query lists, every page.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="query">The filters; its cursor and limit are ignored.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The file.</returns>
    Task<Result<CalendarFile>> ExportAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default);
}
