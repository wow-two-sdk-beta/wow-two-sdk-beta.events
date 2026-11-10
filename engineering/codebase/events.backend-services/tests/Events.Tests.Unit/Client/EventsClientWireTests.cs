using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Unit.Client;

/// <summary>The client speaks the HTTP mirror: its routes and query names, camelCase string enums both ways, the
/// <c>{ data }</c> envelope, and the calendar's bare file.</summary>
public sealed class EventsClientWireTests : IDisposable
{
    private readonly StubEventsService service = new();

    [Fact]
    public async Task Catalog_ShouldReadTheEnvelope_WithCamelCaseEnums()
    {
        service.Answer(HttpStatusCode.OK, """
            {"data":{"slug":"fashion","name":"Fashion","ownerIssuer":"https://id.test/","ownerOrganizationId":"org-1",
             "visibility":"public","locales":["uz"],"kinds":[],"revision":2,
             "createdAtUtc":"2026-11-10T09:00:00+00:00","updatedAtUtc":"2026-11-10T09:00:00+00:00"}}
            """);

        var catalog = Value(await service.Broker<IEventCatalogsBroker>().GetAsync("fashion"));

        catalog.Visibility.Should().Be(CatalogVisibility.Public);
        catalog.Revision.Should().Be(2);
        service.Requests.Single().Url.Should().Be("https://events.example.test/api/events/v1/catalogs/fashion");
    }

    [Fact]
    public async Task Put_ShouldWriteCamelCaseEnums()
    {
        await service.Broker<IEventCatalogsBroker>().PutAsync("fashion", new CatalogSpec { Name = "Fashion", Visibility = CatalogVisibility.Public });

        var (method, _, body) = service.Requests.Single();
        method.Should().Be(HttpMethod.Put);
        using var json = JsonDocument.Parse(body!);
        json.RootElement.GetProperty("visibility").GetString().Should().Be("public");
        json.RootElement.GetProperty("name").GetString().Should().Be("Fashion");
    }

    [Fact]
    public async Task List_ShouldSendTheQuery_ByTheContractsNames()
    {
        await service.Broker<ICatalogEventsBroker>().ListAsync("fashion", new EventQuery
        {
            From = new DateOnly(2026, 10, 1),
            Status = EventStatus.MovedOnline,
            Review = EventReviewState.Candidate,
            IncludeUndated = false,
            Text = "week",
            Limit = 20,
        });

        service.Requests.Single().Url.Should().Be(
            "https://events.example.test/api/events/v1/catalogs/fashion/events?from=2026-10-01&text=week&status=movedOnline&review=candidate&includeUndated=false&limit=20");
    }

    [Fact]
    public async Task Propose_ShouldPostToTheCandidates()
    {
        await service.Broker<ICatalogEventsBroker>().ProposeAsync("fashion", new EventSpec { Facts = new EventFacts { Title = new Dictionary<string, string> { ["und"] = "A" } } });

        var (method, url, _) = service.Requests.Single();
        method.Should().Be(HttpMethod.Post);
        url.Should().Be("https://events.example.test/api/events/v1/catalogs/fashion/candidates");
    }

    [Fact]
    public async Task Writes_ShouldNameTheRevision_InTheQuery()
    {
        var id = Guid.Parse("0193f3a4-0000-7000-8000-000000000001");
        await service.Broker<ICatalogEventsBroker>().ArchiveAsync("fashion", id, 3);
        await service.Broker<IEventTrackingBroker>().DeleteAsync("fashion", id, 4);
        await service.Broker<IEventTrackingBroker>().GetSummaryAsync("fashion", new DateOnly(2026, 11, 10));

        service.Requests.Select(request => $"{request.Method} {request.Url}").Should().Equal(
            $"DELETE https://events.example.test/api/events/v1/catalogs/fashion/events/{id}?expectedRevision=3",
            $"DELETE https://events.example.test/api/events/v1/catalogs/fashion/tracking/{id}?expectedRevision=4",
            "GET https://events.example.test/api/events/v1/catalogs/fashion/tracking/summary?today=2026-11-10");
    }

    [Fact]
    public async Task Import_ShouldPassTheLegacyRecordThrough()
    {
        using var legacy = JsonDocument.Parse("""{"id":42,"date_precision":"estimated"}""");
        await service.Broker<IEventImportsBroker>().ImportAsync("fashion", new EventImportBundle
        {
            Source = "ocharo-desk",
            Rows = [new EventImportRow { LegacyId = "42", Facts = new EventFacts { Title = new Dictionary<string, string> { ["und"] = "A" } }, Legacy = legacy.RootElement.Clone() }],
        }, dryRun: true);

        var (_, url, body) = service.Requests.Single();
        url.Should().EndWith("/catalogs/fashion/imports?dryRun=true");
        using var json = JsonDocument.Parse(body!);
        json.RootElement.GetProperty("rows")[0].GetProperty("legacy").GetProperty("date_precision").GetString().Should().Be("estimated");
    }

    [Fact]
    public async Task Calendar_ShouldReturnTheFile_WithoutAnEnvelope()
    {
        service.Answer(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n", Encoding.UTF8, "text/calendar") };
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "fashion.ics" };
            return response;
        });

        var file = Value(await service.Broker<IEventCalendarBroker>().ExportAsync("fashion", new EventQuery { Cursor = "ignored", Kind = "runway" }));

        file.FileName.Should().Be("fashion.ics");
        file.Content.Should().StartWith("BEGIN:VCALENDAR");
        service.Requests.Single().Url.Should().Be("https://events.example.test/api/events/v1/catalogs/fashion/events.ics?kind=runway");
    }

    public void Dispose() => service.Dispose();

    private static T Value<T>(Result<T> result) where T : notnull =>
        result.Should().BeOfType<Result<T>.Success>().Subject.Value;
}
