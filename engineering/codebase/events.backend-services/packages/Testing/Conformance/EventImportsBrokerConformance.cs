using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventImportsBroker"/>: the owner organization imports; the whole bundle validates
/// before any write; a dry run writes nothing; a rerun finds every row by its old id and returns the same ids.</summary>
public abstract class EventImportsBrokerConformance : EventsConformance
{
    /// <summary>An import creates the events and the importing organization's tracking; a rerun changes nothing and returns
    /// the same old → new map.</summary>
    [Fact]
    public async Task ImportAsync_ShouldReturnTheSameIds_WhenRerun()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var bundle = Bundle("row-1", "row-2");

        var first = Value(await As(owner).Imports.ImportAsync(catalog.Slug, bundle, dryRun: false));
        var rerun = Value(await As(owner).Imports.ImportAsync(catalog.Slug, bundle, dryRun: false));

        first.Created.Should().Be(2);
        first.Failed.Should().Be(0);
        rerun.Created.Should().Be(0);
        rerun.Unchanged.Should().Be(2);
        rerun.Map.Select(Ids).Should().BeEquivalentTo(first.Map.Select(Ids));
        first.Map.Should().OnlyContain(entry => entry.TrackingId != null);

        var imported = first.Map.Single(entry => entry.LegacyId == "row-1");
        var tracked = Value(await As(owner).Tracking.GetAsync(catalog.Slug, imported.EventId));
        tracked.Event.Review.Should().Be(EventReviewState.Published);
        tracked.Event.Facts.Starts.Precision.Should().Be(DatePrecision.Month);
        tracked.Event.Facts.Starts.Certainty.Should().Be(DateCertainty.Estimated);
        tracked.Tracking!.Id.Should().Be(imported.TrackingId!.Value);
        tracked.Tracking.Priority.Should().Be(TrackingPriority.High);
    }

    /// <summary>A dry run reports what it would do and writes nothing.</summary>
    [Fact]
    public async Task ImportAsync_ShouldWriteNothing_OnADryRun()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);

        var report = Value(await As(owner).Imports.ImportAsync(catalog.Slug, Bundle("row-1"), dryRun: true));

        report.DryRun.Should().BeTrue();
        report.Created.Should().Be(1);
        Value(await As(owner).Events.ListAsync(catalog.Slug, new EventQuery())).Items.Should().BeEmpty();
    }

    /// <summary>One invalid row fails the whole bundle before any write.</summary>
    [Fact]
    public async Task ImportAsync_ShouldFailValidation_BeforeAnyWrite_WhenARowIsInvalid()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var bundle = Bundle("row-1");
        bundle = bundle with { Rows = [.. bundle.Rows, Row("row-2") with { Facts = Facts() with { Title = new Dictionary<string, string>() } }] };

        var report = await As(owner).Imports.ImportAsync(catalog.Slug, bundle, dryRun: false);

        Error(report).Type.Should().Be(AppErrorType.Validation);
        Value(await As(owner).Events.ListAsync(catalog.Slug, new EventQuery())).Items.Should().BeEmpty();
    }

    /// <summary>Another organization imports nothing into a private catalog: 404.</summary>
    [Fact]
    public async Task ImportAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var catalog = await CreateCatalogAsync(NewMember());

        Error(await As(NewMember()).Imports.ImportAsync(catalog.Slug, Bundle("row-1"), dryRun: false)).Type.Should().Be(AppErrorType.NotFound);
    }

    private static (string LegacyId, Guid EventId, Guid? TrackingId) Ids(EventImportMapEntry entry) => (entry.LegacyId, entry.EventId, entry.TrackingId);

    private static EventImportBundle Bundle(params string[] legacyIds) => new()
    {
        Source = "conformance",
        Rows = [.. legacyIds.Select(Row)],
    };

    private static EventImportRow Row(string legacyId)
    {
        using var legacy = JsonDocument.Parse($$"""{"id":"{{legacyId}}","date_precision":"estimated"}""");
        return new EventImportRow
        {
            LegacyId = legacyId,
            Facts = Facts($"Imported {legacyId}", "2026-10", DatePrecision.Month) with
            {
                Starts = new EventWhen { Value = "2026-10", Precision = DatePrecision.Month, Certainty = DateCertainty.Estimated },
            },
            SourceUrl = $"https://example.com/events/{legacyId}",
            RecordedAtUtc = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero),
            Legacy = legacy.RootElement.Clone(),
            Tracking = new EventImportTracking { Stage = "interested", Priority = TrackingPriority.High, Notes = "From the old desk" },
        };
    }
}
