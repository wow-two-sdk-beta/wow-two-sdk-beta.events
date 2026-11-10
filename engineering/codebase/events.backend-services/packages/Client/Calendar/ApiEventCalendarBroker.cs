using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Calendar;

/// <summary>Integrates the running Events service's calendar export: the answer is the <c>text/calendar</c> file itself,
/// with no envelope.</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiEventCalendarBroker(EventsApiTransport transport) : IEventCalendarBroker
{
    /// <inheritdoc />
    public async Task<Result<CalendarFile>> ExportAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default)
    {
        var route = EventQueryMapper.ToQuery(query, withPage: false).ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Calendar));
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        var sent = await transport.SendRawAsync(request, cancellationToken);
        if (sent is Result<HttpResponseMessage>.Failure failure)
            return Result<CalendarFile>.Fail(failure.Error);

        using var response = ((Result<HttpResponseMessage>.Success)sent).Value;
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var disposition = response.Content.Headers.ContentDisposition;
        var name = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
        return Result<CalendarFile>.Ok(new CalendarFile(string.IsNullOrWhiteSpace(name) ? $"{catalog}.ics" : name, content));
    }
}
