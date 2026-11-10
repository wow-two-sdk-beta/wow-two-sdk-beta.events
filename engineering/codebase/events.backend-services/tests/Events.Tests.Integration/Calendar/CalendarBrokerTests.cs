using AwesomeAssertions;
using Events.Tests.Integration.Infrastructure;
using Events.Tests.Integration.Tracking;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace Events.Tests.Integration.Calendar;

/// <summary>Covers the module's calendar export over stored events: what it reads and the filters it applies.</summary>
/// <param name="fixture">The module over PostgreSQL.</param>
public sealed class CalendarBrokerTests(ModuleFixture fixture) : IClassFixture<ModuleFixture>
{
    [Fact]
    public async Task ExportAsync_ShouldWriteThePublishedEventsAQueryLists_WhenTheCatalogIsReadable()
    {
        var catalog = await CatalogSeed.CatalogAsync(fixture, ModuleFixture.NewOrganization(), CatalogVisibility.Public, "Ocharo, fashion");
        var day = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Runway night", "2026-12-01", deadlines: [CatalogSeed.Deadline("2026-11-20")]));
        var month = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("October fair", "2026-10", DatePrecision.Month));
        var candidate = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Proposed show", "2026-12-02"), EventReviewState.Candidate);
        var early = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Summer bazaar", "2026-06-02"));
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        await using var scope = fixture.Scope();
        var broker = scope.ServiceProvider.GetRequiredService<IEventCalendarBroker>();

        var file = (await broker.ExportAsync(catalog.Slug, new EventQuery { From = new DateOnly(2026, 9, 1), Limit = 1 })).Value();

        file.FileName.Should().Be($"{catalog.Slug}.ics");
        file.Content.Should().StartWith("BEGIN:VCALENDAR\r\n").And.EndWith("END:VCALENDAR\r\n");
        file.Content.Should().Contain("X-WR-CALNAME:Ocharo\\, fashion\r\n");
        file.Content.Should().Contain($"UID:{day.Id}@events\r\n").And.Contain($"UID:{day.Id}-deadline-0@events\r\n");
        file.Content.Should().Contain("DTSTART;VALUE=DATE:20261201\r\nDTEND;VALUE=DATE:20261202\r\n");
        file.Content.Should().NotContain(month.Id.ToString(), "a month start writes no entry");
        file.Content.Should().NotContain(candidate.Id.ToString(), "the export reads published events only");
        file.Content.Should().NotContain(early.Id.ToString(), "the query's filters apply");
    }

    [Fact]
    public async Task ExportAsync_ShouldFailWithCatalogNotFound_WhenTheCallerCannotReadTheCatalog()
    {
        var catalog = await CatalogSeed.CatalogAsync(fixture, ModuleFixture.NewOrganization());
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        await using var scope = fixture.Scope();

        (await scope.ServiceProvider.GetRequiredService<IEventCalendarBroker>().ExportAsync(catalog.Slug, new EventQuery()))
            .Error().Type.Should().Be(AppErrorType.NotFound);
    }
}
