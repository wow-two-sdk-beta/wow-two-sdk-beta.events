using AwesomeAssertions;
using Events.Tests.Integration.Infrastructure;
using Events.Tests.Integration.Tracking;
using Events.Tests.Integration.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace Events.Tests.Integration.Imports;

/// <summary>Covers the module's imports with Desk-shaped bundles: the id map, dry runs, honest dates, validation and evidence.</summary>
/// <param name="fixture">The module over PostgreSQL.</param>
public sealed class ImportsBrokerTests(ModuleFixture fixture) : IClassFixture<ModuleFixture>
{
    [Fact]
    public async Task ImportAsync_ShouldReturnTheSameIdMap_WhenTheBundleIsImportedTwice()
    {
        var catalog = await OcharoCatalogAsync();
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        var bundle = DeskBundle.Create();

        var first = (await broker.ImportAsync(catalog.Slug, bundle, dryRun: false)).Value();
        var second = (await broker.ImportAsync(catalog.Slug, bundle, dryRun: false)).Value();

        first.Created.Should().Be(3);
        first.Map.Select(entry => entry.LegacyId).Should().Equal(bundle.Rows.Select(row => row.LegacyId));
        first.Map[0].TrackingId.Should().NotBeNull();
        first.Map[2].TrackingId.Should().BeNull("the row carries no tracking");
        second.Unchanged.Should().Be(3);
        second.Map.Select(entry => (entry.LegacyId, entry.EventId, entry.TrackingId))
            .Should().Equal(first.Map.Select(entry => (entry.LegacyId, entry.EventId, entry.TrackingId)));

        var changed = bundle with
        {
            Rows = [bundle.Rows[0] with { Tracking = bundle.Rows[0].Tracking! with { Stage = "applying" } }, bundle.Rows[1], bundle.Rows[2] with
            {
                Facts = bundle.Rows[2].Facts with { Starts = new EventWhen { Value = "2026-12-06", Precision = DatePrecision.Day } },
            }],
        };
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        var third = (await broker.ImportAsync(catalog.Slug, changed, dryRun: false)).Value();

        third.Map.Select(entry => entry.Outcome).Should().Equal(EventImportOutcome.Updated, EventImportOutcome.Unchanged, EventImportOutcome.Updated);
        third.Map.Select(entry => entry.EventId).Should().Equal(first.Map.Select(entry => entry.EventId));
        var tracking = Tracking(scope);
        var day = (await tracking.GetAsync(catalog.Slug, first.Map[2].EventId)).Value().Event;
        day.Revision.Should().Be(2);
        day.Facts.Starts.Value.Should().Be("2026-12-06");
        (await tracking.GetHistoryAsync(catalog.Slug, first.Map[0].EventId)).Value()
            .Select(entry => (entry.FromStage, entry.ToStage)).Should().Equal((null, "shortlisted"), ("shortlisted", "applying"));
    }

    [Fact]
    public async Task ImportAsync_ShouldWriteNothing_WhenItIsADryRun()
    {
        var catalog = await OcharoCatalogAsync();
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        var report = (await broker.ImportAsync(catalog.Slug, DeskBundle.Create(), dryRun: true)).Value();

        report.DryRun.Should().BeTrue();
        report.Created.Should().Be(3);
        report.Map.Should().OnlyContain(entry => entry.EventId != Guid.Empty);
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        (await db.Events.CountAsync(row => row.CatalogId == catalog.Id)).Should().Be(0);
        (await db.ImportMap.CountAsync(row => row.CatalogId == catalog.Id)).Should().Be(0);
        (await db.Sources.CountAsync(row => row.CatalogId == catalog.Id)).Should().Be(0);
        (await db.Observations.CountAsync(row => row.CatalogId == catalog.Id)).Should().Be(0);
        (await db.Tracking.CountAsync(row => row.OrganizationId == catalog.OwnerOrganizationId)).Should().Be(0);

        (await broker.ImportAsync(catalog.Slug, DeskBundle.Create(), dryRun: false)).Value().Created.Should().Be(3);
        var rerun = (await broker.ImportAsync(catalog.Slug, DeskBundle.Create(), dryRun: true)).Value();
        rerun.Unchanged.Should().Be(3, "a dry run after the import finds every row through the map");
    }

    [Fact]
    public async Task ImportAsync_ShouldKeepMonthPrecisionAndEstimatedCertainty_WhenRowsCarryThem()
    {
        var catalog = await OcharoCatalogAsync();
        await using var scope = fixture.Scope();
        var report = (await Broker(scope).ImportAsync(catalog.Slug, DeskBundle.Create(), dryRun: false)).Value();
        var tracking = Tracking(scope);

        var month = (await tracking.GetAsync(catalog.Slug, report.Map[0].EventId)).Value();
        month.Event.Review.Should().Be(EventReviewState.Published);
        month.Event.PublishedAtUtc.Should().Be(DeskBundle.MonthRow().RecordedAtUtc);
        month.Event.Facts.Starts.Should().Be(new EventWhen { Value = "2026-10", Precision = DatePrecision.Month, Certainty = DateCertainty.Estimated });
        month.Event.Facts.Ends.Should().BeNull();
        month.Event.Facts.Title.Should().BeEquivalentTo(new Dictionary<string, string> { ["und"] = "Tashkent Fashion Week" });
        month.Event.Facts.Place!.Country.Should().Be("UZ");
        month.Event.Sources.Should().ContainSingle().Which.Should().Match<EventSourceModel>(source =>
            source.Kind == EventSourceKind.Import && source.Id == "ocharo-desk/0193a1b2-0001" && source.Url == "https://desk.example/showcase/1");
        month.Tracking!.Stage.Should().Be("shortlisted");
        month.Tracking.Priority.Should().Be(TrackingPriority.High);
        month.Tracking.Fields!.Value.GetProperty("cost").GetString().Should().Be("1500 USD");

        var undated = (await tracking.GetAsync(catalog.Slug, report.Map[1].EventId)).Value();
        undated.Event.Facts.Starts.Precision.Should().Be(DatePrecision.Unknown);
        undated.Event.Facts.Starts.Value.Should().BeNull();

        var lateOctober = (await tracking.ListAsync(catalog.Slug, new TrackingQuery { From = new DateOnly(2026, 10, 25), To = new DateOnly(2026, 10, 31), IncludeUndated = false })).Value();
        lateOctober.Items.Select(item => item.Event.Id).Should().Equal(report.Map[0].EventId);
    }

    [Fact]
    public async Task ImportAsync_ShouldRejectTheWholeBundle_WhenOneRowIsInvalid()
    {
        var catalog = await OcharoCatalogAsync();
        await using var scope = fixture.Scope();
        var bundle = DeskBundle.Create();
        var invalid = bundle with
        {
            Rows = [bundle.Rows[0], bundle.Rows[1] with { Tracking = bundle.Rows[1].Tracking! with { Stage = "interested" } }, bundle.Rows[2] with
            {
                Facts = bundle.Rows[2].Facts with { Title = new Dictionary<string, string>() },
            }],
        };

        var error = (await Broker(scope).ImportAsync(catalog.Slug, invalid, dryRun: false)).Error();

        error.Type.Should().Be(AppErrorType.Validation);
        error.Message.Should().Contain("rows[1] (0193a1b2-0002): tracking.stage").And.Contain("rows[2] (0193a1b2-0003): facts.title");
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        (await db.Events.CountAsync(row => row.CatalogId == catalog.Id)).Should().Be(0);

        var repeated = bundle with { Rows = [bundle.Rows[0], bundle.Rows[0]] };
        (await Broker(scope).ImportAsync(catalog.Slug, repeated, dryRun: true)).Error().Message.Should().Contain("legacyId: repeats rows[0]");
        (await Broker(scope).ImportAsync(catalog.Slug, bundle with { Format = "csv" }, dryRun: true)).Error().Message.Should().StartWith("format");
        (await Broker(scope).ImportAsync(catalog.Slug, bundle with { Rows = [] }, dryRun: true)).Error().Message.Should().StartWith("rows");
    }

    [Fact]
    public async Task ImportAsync_ShouldKeepTheLegacyRowAsEvidence()
    {
        var catalog = await OcharoCatalogAsync();
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        var bundle = DeskBundle.Create();

        var first = (await broker.ImportAsync(catalog.Slug, bundle, dryRun: false)).Value();
        var unchanged = (await broker.ImportAsync(catalog.Slug, bundle, dryRun: false)).Value();
        var edited = bundle with
        {
            Rows = [bundle.Rows[0] with { Legacy = DeskBundle.Json("""{ "id": "0193a1b2-0001", "status": "applying" }""") }, bundle.Rows[1], bundle.Rows[2]],
        };
        var second = (await broker.ImportAsync(catalog.Slug, edited, dryRun: false)).Value();

        unchanged.Unchanged.Should().Be(3);
        second.Map[0].Outcome.Should().Be(EventImportOutcome.Updated);
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var evidence = await db.Observations.AsNoTracking()
            .Where(row => row.CatalogId == catalog.Id)
            .OrderBy(row => row.Seq)
            .ToListAsync();
        evidence.Should().HaveCount(2, "only the row with a legacy record keeps evidence, and its unchanged rerun adds none");
        evidence.Should().OnlyContain(row => row.EventId == first.Map[0].EventId && row.SourceKind == "import"
            && row.Type == "import.row" && row.State == "recorded" && row.Links == "[]" && row.SubscriptionId == null);
        evidence[0].ObservedAt.Should().Be(DeskBundle.MonthRow().RecordedAtUtc!.Value);
        DeskBundle.Json(evidence[0].Payload!).GetProperty("status").GetString().Should().Be("shortlisted");
        DeskBundle.Json(evidence[1].Payload!).GetProperty("status").GetString().Should().Be("applying");
    }

    [Fact]
    public async Task ImportAsync_ShouldFailOnlyThatRow_WhenItsKeyIsTakenByAnotherEvent()
    {
        var catalog = await OcharoCatalogAsync();
        await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Curated entry", "2026-10-02", key: "tashkent-fashion-week-2026"));
        await using var scope = fixture.Scope();

        var report = (await Broker(scope).ImportAsync(catalog.Slug, DeskBundle.Create(), dryRun: false)).Value();

        report.Failed.Should().Be(1);
        report.Created.Should().Be(2);
        report.Map[0].Outcome.Should().Be(EventImportOutcome.Failed);
        report.Map[0].Error.Should().Contain("tashkent-fashion-week-2026");
        report.Map[0].EventId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task ImportAsync_ShouldFailWithNotCatalogOwner_WhenAnotherOrganizationImports()
    {
        var catalog = await CatalogSeed.CatalogAsync(fixture, ModuleFixture.NewOrganization(), CatalogVisibility.Public);
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        await using var scope = fixture.Scope();

        (await Broker(scope).ImportAsync(catalog.Slug, DeskBundle.Create(), dryRun: true)).Error().Type.Should().Be(AppErrorType.Forbidden);
    }

    // A catalog owned by a fresh organization acting now, with Ocharo's workflow written first, as Desk does.
    private async Task<CatalogEntity> OcharoCatalogAsync()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization, CatalogVisibility.Private, "Ocharo fashion", "runway", "festival", "fair");
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        (await scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>().PutAsync(OcharoWorkflow.Spec())).Value();
        return catalog;
    }

    private static IEventImportsBroker Broker(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IEventImportsBroker>();

    private static IEventTrackingBroker Tracking(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IEventTrackingBroker>();
}
