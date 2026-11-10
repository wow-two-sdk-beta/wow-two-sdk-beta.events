using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.CatalogEvents.Models;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace Events.Api.Calendar.Controllers;

/// <summary>Exposes <see cref="IEventCalendarBroker"/>: the calendar file itself, no envelope, read as the catalog is
/// (<c>events.read</c>; inside its owner organization, #50).</summary>
/// <param name="calendar">The calendar broker.</param>
[Route(EventsRouteConstants.Catalogs + "/{catalog}")]
public sealed class CalendarController(IEventCalendarBroker calendar) : EventsControllerBase
{
    /// <summary>Holds the file's media type.</summary>
    private const string MediaType = "text/calendar; charset=utf-8";

    /// <summary>Exports a catalog's events as an iCalendar file.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="parameters">The event filters; the page is ignored.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The <c>text/calendar</c> file.</returns>
    [HttpGet(EventsRouteConstants.Calendar)]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [Produces("text/calendar")]
    public async Task<IActionResult> Export(string catalog, [FromQuery] EventQueryParameters parameters, CancellationToken ct)
    {
        if (parameters.TryMap(out var query) is { } error)
            return Invalid(error);

        return (await calendar.ExportAsync(catalog, query, ct)).Match<IActionResult>(
            file => File(Encoding.UTF8.GetBytes(file.Content), MediaType, file.FileName),
            failure => this.ToProblem(failure));
    }
}
