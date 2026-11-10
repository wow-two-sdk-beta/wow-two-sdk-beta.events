using System.Text.Json;
using AwesomeAssertions;
using Events.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Principals;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace Events.Tests.Integration.Tracking;

/// <summary>Covers the module's tracking: privacy per organization, writes, history, revisions, the list and the summary.</summary>
/// <param name="fixture">The module over PostgreSQL.</param>
public sealed class TrackingBrokerTests(ModuleFixture fixture) : IClassFixture<ModuleFixture>
{
    [Fact]
    public async Task ListAsync_ShouldNotShowAnotherOrganizationsTracking_WhenTwoOrganizationsTrackOneEvent()
    {
        var owner = ModuleFixture.NewOrganization();
        var other = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, owner, CatalogVisibility.Public);
        var item = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Tashkent Fashion Week", "2026-12-01"));
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        fixture.Principal.ActAsMember(owner);
        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "going", Notes = "owner secret plan" })).Value();
        fixture.Principal.ActAsMember(other);
        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "interested", Notes = "other notes" })).Value();

        var seen = (await broker.ListAsync(catalog.Slug, new TrackingQuery())).Value().Items.Should().ContainSingle().Which;
        seen.Event.Id.Should().Be(item.Id);
        seen.Tracking!.Stage.Should().Be("interested");
        seen.Tracking.Notes.Should().Be("other notes");
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Text = "secret" })).Value().Items.Should().BeEmpty();
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Stage = "going" })).Value().Items.Should().BeEmpty();
        (await broker.GetHistoryAsync(catalog.Slug, item.Id)).Value().Should().ContainSingle().Which.ToStage.Should().Be("interested");

        fixture.Principal.ActAsMember(owner);
        var own = (await broker.GetAsync(catalog.Slug, item.Id)).Value();
        own.Tracking!.Stage.Should().Be("going");
        own.Tracking.Notes.Should().Be("owner secret plan");
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Text = "secret" })).Value().Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ListAsync_ShouldFailWithOrganizationRequired_WhenTheCallerHasNoOrganization()
    {
        var catalog = await CatalogSeed.CatalogAsync(fixture, ModuleFixture.NewOrganization(), CatalogVisibility.Public);
        fixture.Principal.ActAsAnonymous();
        await using var scope = fixture.Scope();

        (await Broker(scope).ListAsync(catalog.Slug, new TrackingQuery())).Error().Type.Should().Be(AppErrorType.Forbidden);
    }

    [Fact]
    public async Task ListAsync_ShouldFailWithCatalogNotFound_WhenAnotherOrganizationOwnsThePrivateCatalog()
    {
        var catalog = await CatalogSeed.CatalogAsync(fixture, ModuleFixture.NewOrganization());
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        await using var scope = fixture.Scope();

        (await Broker(scope).ListAsync(catalog.Slug, new TrackingQuery())).Error().Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task ListAsync_ShouldFilterOrderAndPage_WhenAskedForASlice()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization);
        var undated = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Undated show"));
        var month = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("October fair", "2026-10", DatePrecision.Month));
        var early = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Bazaar", "2026-09-01", deadlines: [CatalogSeed.Deadline("2026-11-20")]));
        var late = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Atelier days", "2026-12-05"));
        await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Candidate show", "2026-12-06"), EventReviewState.Candidate);
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        (await broker.PutAsync(catalog.Slug, late.Id, new TrackingSpec { Stage = "going", Priority = TrackingPriority.High })).Value();

        var all = (await broker.ListAsync(catalog.Slug, new TrackingQuery())).Value();
        all.Items.Select(item => item.Event.Id).Should().Equal(early.Id, month.Id, late.Id, undated.Id);

        var first = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Limit = 3 })).Value();
        first.Items.Should().HaveCount(3);
        var second = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Limit = 3, Cursor = first.NextCursor })).Value();
        second.Items.Select(item => item.Event.Id).Should().Equal(undated.Id);
        second.NextCursor.Should().BeNull();
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Cursor = "not-a-cursor" })).Error().Type.Should().Be(AppErrorType.Validation);

        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { From = new DateOnly(2026, 10, 20), IncludeUndated = false }))
            .Should().Equal(month.Id, late.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { To = new DateOnly(2026, 10, 1), IncludeUndated = false }))
            .Should().Equal(early.Id, month.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { DeadlineBefore = new DateOnly(2026, 11, 30) })).Should().Equal(early.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { TrackedOnly = true })).Should().Equal(late.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Priority = TrackingPriority.High })).Should().Equal(late.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Text = "BAZAAR" })).Should().Equal(early.Id);
    }

    [Fact]
    public async Task PutAsync_ShouldStartInTheDefaultStageWithHistory_WhenNoStageIsGiven()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization);
        var item = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Runway night", "2026-12-01"));
        fixture.Principal.ActAsMember(organization, "person-7");
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        using var fields = JsonDocument.Parse("""{ "participation": "exhibitor", "cost": "1200 USD" }""");

        var tracking = (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec
        {
            Assignee = "person-9",
            Notes = "Book the booth",
            Fields = fields.RootElement,
            References = [new TrackingReference("ocharo.campaign", "c-1")],
        })).Value();

        tracking.Stage.Should().Be("interested");
        tracking.Revision.Should().Be(1);
        tracking.Fields!.Value.GetProperty("cost").GetString().Should().Be("1200 USD");
        tracking.References.Should().Equal(new TrackingReference("ocharo.campaign", "c-1"));
        var entry = (await broker.GetHistoryAsync(catalog.Slug, item.Id)).Value().Should().ContainSingle().Which;
        entry.FromStage.Should().BeNull();
        entry.ToStage.Should().Be("interested");
        entry.ActorIssuer.Should().Be(TestPrincipal.DefaultIssuer);
        entry.ActorSubject.Should().Be("person-7");
    }

    [Fact]
    public async Task PutAsync_ShouldAppendHistoryOnlyForStageChanges_WhenTrackingChanges()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization);
        var item = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Craft fair", "2026-12-01"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "interested" })).Value();
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        var moved = (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "going", ExpectedRevision = 1 })).Value();
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        var noted = (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "going", Notes = "Tickets bought", ExpectedRevision = 2 })).Value();
        var same = (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "going", Notes = "Tickets bought", ExpectedRevision = 3 })).Value();

        moved.Revision.Should().Be(2);
        noted.Revision.Should().Be(3);
        same.Revision.Should().Be(3, "a write that changes nothing keeps the revision");
        (await broker.GetHistoryAsync(catalog.Slug, item.Id)).Value()
            .Select(entry => (entry.FromStage, entry.ToStage)).Should().Equal((null, "interested"), ("interested", "going"));
    }

    [Fact]
    public async Task PutAsync_ShouldFailWithRevisionConflict_WhenTheRevisionIsMissingOrStale()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization);
        var item = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Design week", "2026-12-01"));
        var untracked = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Textile expo", "2026-12-02"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec())).Value();

        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "going" })).Error().Type.Should().Be(AppErrorType.Conflict);
        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "going", ExpectedRevision = 7 })).Error().Type.Should().Be(AppErrorType.Conflict);
        (await broker.PutAsync(catalog.Slug, untracked.Id, new TrackingSpec { ExpectedRevision = 3 })).Error().Type.Should().Be(AppErrorType.Conflict);
        (await broker.DeleteAsync(catalog.Slug, item.Id, 9)).Error().Type.Should().Be(AppErrorType.Conflict);
        (await broker.GetAsync(catalog.Slug, item.Id)).Value().Tracking!.Stage.Should().Be("interested");
    }

    [Fact]
    public async Task PutAsync_ShouldFailWithValidation_WhenTheStageOrAFieldIsInvalid()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization);
        var item = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Photo week", "2026-12-01"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        using var array = JsonDocument.Parse("[1]");

        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "shortlisted" })).Error().Message.Should().Contain("'shortlisted'");
        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Notes = new string('n', 8_001) })).Error().Message.Should().StartWith("notes");
        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Fields = array.RootElement })).Error().Message.Should().StartWith("fields");
        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { References = [new TrackingReference("", "x")] }))
            .Error().Message.Should().StartWith("references[0]");
        (await broker.PutAsync(catalog.Slug, Guid.CreateVersion7(), new TrackingSpec())).Error().Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task PutAsync_ShouldTrackTheOwnersCandidate_WhileNeverTrackingArchivedOrOthersCandidates()
    {
        var owner = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, owner, CatalogVisibility.Public);
        var candidate = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Proposed show", "2026-12-01"), EventReviewState.Candidate);
        var archived = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Old show", "2026-12-01"), EventReviewState.Archived);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        fixture.Principal.ActAsMember(owner);
        (await broker.PutAsync(catalog.Slug, candidate.Id, new TrackingSpec { Stage = "planning" })).Value().Stage.Should().Be("planning");
        (await broker.GetAsync(catalog.Slug, candidate.Id)).Value().Event.Review.Should().Be(EventReviewState.Candidate);
        (await broker.GetHistoryAsync(catalog.Slug, candidate.Id)).Value().Should().ContainSingle();
        (await broker.ListAsync(catalog.Slug, new TrackingQuery())).Value().Items.Should().BeEmpty("lists read published events only");
        (await broker.GetSummaryAsync(catalog.Slug, new DateOnly(2026, 11, 10))).Value().ByStage["planning"].Should().Be(0);
        (await broker.PutAsync(catalog.Slug, archived.Id, new TrackingSpec())).Error().Type.Should().Be(AppErrorType.NotFound);

        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        (await broker.GetAsync(catalog.Slug, candidate.Id)).Error().Type.Should().Be(AppErrorType.NotFound);
        (await broker.PutAsync(catalog.Slug, candidate.Id, new TrackingSpec())).Error().Type.Should().Be(AppErrorType.NotFound);

        fixture.Principal.ActAsMember(owner);
        (await broker.DeleteAsync(catalog.Slug, candidate.Id, 1)).Value().Stage.Should().Be("planning");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheTrackingAndItsHistory_WhenTheRevisionMatches()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization);
        var item = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Fabric fair", "2026-12-01"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        var tracking = (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec())).Value();

        var removed = (await broker.DeleteAsync(catalog.Slug, item.Id, tracking.Revision)).Value();

        removed.Id.Should().Be(tracking.Id);
        (await broker.GetAsync(catalog.Slug, item.Id)).Value().Tracking.Should().BeNull();
        (await broker.GetHistoryAsync(catalog.Slug, item.Id)).Value().Should().BeEmpty();
        (await broker.DeleteAsync(catalog.Slug, item.Id, tracking.Revision)).Error().Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldCountEveryStageAndListOpenDeadlines_WhenTheOrganizationTracksEvents()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization);
        var planned = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts(
            "Planned show", "2027-01-10", deadlines: [CatalogSeed.Deadline("2026-12-01"), CatalogSeed.Deadline("2026-11-15", DeadlineKind.Registration)]));
        var skipped = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Skipped show", "2027-01-11", deadlines: [CatalogSeed.Deadline("2026-11-12")]));
        var past = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Past deadline", "2027-01-12", deadlines: [CatalogSeed.Deadline("2026-11-01")]));
        await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Untracked", "2027-01-13", deadlines: [CatalogSeed.Deadline("2026-11-11")]));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        (await broker.PutAsync(catalog.Slug, planned.Id, new TrackingSpec { Stage = "planning" })).Value();
        (await broker.PutAsync(catalog.Slug, skipped.Id, new TrackingSpec { Stage = "skipped" })).Value();
        (await broker.PutAsync(catalog.Slug, past.Id, new TrackingSpec { Stage = "going" })).Value();

        var summary = (await broker.GetSummaryAsync(catalog.Slug, new DateOnly(2026, 11, 10))).Value();

        summary.ByStage.Keys.Should().Equal("interested", "planning", "going", "attended", "skipped");
        summary.ByStage.Values.Should().Equal(0, 1, 1, 0, 1);
        summary.UpcomingDeadlines.Select(deadline => (deadline.EventId, deadline.Deadline.At.Value, deadline.Stage)).Should().Equal(
            (planned.Id, "2026-11-15", "planning"), (planned.Id, "2026-12-01", "planning"));
        summary.UpcomingDeadlines[0].Deadline.Kind.Should().Be(DeadlineKind.Registration);
        summary.UpcomingDeadlines[0].Title["en"].Should().Be("Planned show");
    }

    private static IReadOnlyList<Guid> Ids(WoW.Two.Sdk.Beta.Be.Foundation.Results.Result<TrackingPage> result) =>
        result.Value().Items.Select(item => item.Event.Id).ToList();

    private static IEventTrackingBroker Broker(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IEventTrackingBroker>();
}
