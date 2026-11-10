using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventCalendarBroker"/>: read as the catalog is, inside its owner organization; each
/// event exports with a stable UID, a day start as all-day, a month start not at all.</summary>
public abstract class EventCalendarBrokerConformance : EventsConformance
{
    /// <summary>A day event exports under its stable UID.</summary>
    [Fact]
    public async Task ExportAsync_ShouldExportADayEvent_UnderItsStableUid()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var day = await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12-05"));

        var file = Value(await As(owner).Calendar.ExportAsync(catalog.Slug, new EventQuery()));

        file.FileName.Should().EndWith(".ics");
        file.Content.Should().Contain("BEGIN:VCALENDAR").And.Contain($"UID:{day.Id}@events");
    }

    /// <summary>A month start exports no entry.</summary>
    [Fact]
    public async Task ExportAsync_ShouldLeaveOut_AMonthPrecisionStart()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var month = await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12", precision: DatePrecision.Month));

        var file = Value(await As(owner).Calendar.ExportAsync(catalog.Slug, new EventQuery()));

        file.Content.Should().NotContain($"UID:{month.Id}@events");
    }

    /// <summary>Another organization exports nothing of a private catalog: 404.</summary>
    [Fact]
    public async Task ExportAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12-05"));

        Error(await As(NewMember()).Calendar.ExportAsync(catalog.Slug, new EventQuery())).Type.Should().Be(AppErrorType.NotFound);
    }
}
