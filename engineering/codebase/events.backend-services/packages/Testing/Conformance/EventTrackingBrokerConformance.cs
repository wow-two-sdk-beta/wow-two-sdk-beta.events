using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventTrackingBroker"/>: tracking belongs to the caller's active organization —
/// shared by its members, invisible to every other organization — moves through the organization's workflow at the
/// revision read, and records each stage change.</summary>
public abstract class EventTrackingBrokerConformance : EventsConformance
{
    /// <summary>Holds the default workflow's first stage.</summary>
    private const string DefaultStage = "interested";

    /// <summary>An event nobody tracks reads with no tracking.</summary>
    [Fact]
    public async Task GetAsync_ShouldReturnTheEvent_WithNoTracking_BeforeAnyPut()
    {
        var (owner, catalog, target) = await EventAsync();

        var tracked = Value(await As(owner).Tracking.GetAsync(catalog, target.Id));

        tracked.Event.Id.Should().Be(target.Id);
        tracked.Tracking.Should().BeNull();
    }

    /// <summary>A put without a stage starts in the workflow's default stage, at revision 1.</summary>
    [Fact]
    public async Task PutAsync_ShouldStartInTheDefaultStage()
    {
        var (owner, catalog, target) = await EventAsync();

        var tracking = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Priority = TrackingPriority.High, Notes = "Apply early" }));

        tracking.EventId.Should().Be(target.Id);
        tracking.Stage.Should().Be(DefaultStage);
        tracking.Priority.Should().Be(TrackingPriority.High);
        tracking.Notes.Should().Be("Apply early");
        tracking.Revision.Should().Be(1);
        Value(await As(owner).Tracking.GetAsync(catalog, target.Id)).Tracking!.Id.Should().Be(tracking.Id);
    }

    /// <summary>A put at a stale revision conflicts.</summary>
    [Fact]
    public async Task PutAsync_ShouldFailConflict_WhenTheRevisionIsStale()
    {
        var (owner, catalog, target) = await EventAsync();
        var first = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec()));
        Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Stage = "planning", ExpectedRevision = first.Revision }));

        var stale = await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Stage = "going", ExpectedRevision = first.Revision });

        Error(stale).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>A stage outside the organization's workflow is invalid.</summary>
    [Fact]
    public async Task PutAsync_ShouldFailValidation_WhenTheStageIsUnknown()
    {
        var (owner, catalog, target) = await EventAsync();

        Error(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Stage = "no-such-stage" })).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>Every member of the organization sees the same tracking.</summary>
    [Fact]
    public async Task Tracking_ShouldBeSharedByTheOrganizationsMembers()
    {
        var (owner, catalog, target) = await EventAsync();
        var tracking = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Notes = "Shared" }));

        var colleague = Value(await As(NewMember(owner.OrganizationId)).Tracking.GetAsync(catalog, target.Id));

        colleague.Tracking!.Id.Should().Be(tracking.Id);
        colleague.Tracking.Notes.Should().Be("Shared");
    }

    /// <summary>Another organization never sees the owner's tracking, even of an event it reads in a public catalog; its
    /// own tracking leaves the owner's untouched.</summary>
    [Fact]
    public async Task Tracking_ShouldStayInsideTheOrganization()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner, CatalogVisibility.Public);
        var target = await CreateEventAsync(owner, catalog.Slug);
        var tracking = Value(await As(owner).Tracking.PutAsync(catalog.Slug, target.Id, new TrackingSpec { Notes = "Owner's notes" }));
        var other = As(NewMember());

        var seen = await other.Tracking.GetAsync(catalog.Slug, target.Id);

        if (seen is Result<TrackedEventModel>.Success { Value: var read })
        {
            read.Tracking.Should().BeNull("another organization's tracking never answers");
            Value(await other.Tracking.PutAsync(catalog.Slug, target.Id, new TrackingSpec { Notes = "Theirs" })).Id.Should().NotBe(tracking.Id);
        }
        else
        {
            Error(seen).Type.Should().Be(AppErrorType.NotFound);
        }

        Value(await As(owner).Tracking.GetAsync(catalog.Slug, target.Id)).Tracking!.Should().BeEquivalentTo(tracking);
    }

    /// <summary>Another organization reaches no tracking of a private catalog: 404.</summary>
    [Fact]
    public async Task GetAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var (_, catalog, target) = await EventAsync();

        Error(await As(NewMember()).Tracking.GetAsync(catalog, target.Id)).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>A caller without an organization, such as a service acting as itself, tracks nothing.</summary>
    [Fact]
    public async Task ListAsync_ShouldFailForbidden_WithoutAnOrganization()
    {
        var (_, catalog, _) = await EventAsync();

        Error(await As(EventsActor.Service()).Tracking.ListAsync(catalog, new TrackingQuery())).Type.Should().Be(AppErrorType.Forbidden);
    }

    /// <summary>A delete at the revision read removes the tracking; a second one finds none.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheTracking_ThenFailNotFound()
    {
        var (owner, catalog, target) = await EventAsync();
        var tracking = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec()));

        Value(await As(owner).Tracking.DeleteAsync(catalog, target.Id, tracking.Revision)).Id.Should().Be(tracking.Id);
        Error(await As(owner).Tracking.DeleteAsync(catalog, target.Id, tracking.Revision)).Type.Should().Be(AppErrorType.NotFound);
        Value(await As(owner).Tracking.GetAsync(catalog, target.Id)).Tracking.Should().BeNull();
    }

    /// <summary>Each stage change lands in the history with its actor.</summary>
    [Fact]
    public async Task GetHistoryAsync_ShouldRecordEachStageChange()
    {
        var (owner, catalog, target) = await EventAsync();
        var first = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec()));
        Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Stage = "planning", ExpectedRevision = first.Revision }));

        var history = Value(await As(owner).Tracking.GetHistoryAsync(catalog, target.Id));

        history.Select(entry => entry.ToStage).Should().Equal(DefaultStage, "planning");
        history[^1].FromStage.Should().Be(DefaultStage);
        history[^1].ActorSubject.Should().Be(owner.Subject);
    }

    /// <summary>The summary counts the organization's tracking per stage, and lists tracked lists only with the filter.</summary>
    [Fact]
    public async Task GetSummaryAsync_ShouldCountTheTrackingPerStage()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var first = await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12-01"));
        var second = await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12-02"));
        await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12-03"));
        Value(await As(owner).Tracking.PutAsync(catalog.Slug, first.Id, new TrackingSpec()));
        Value(await As(owner).Tracking.PutAsync(catalog.Slug, second.Id, new TrackingSpec { Stage = "planning" }));

        var summary = Value(await As(owner).Tracking.GetSummaryAsync(catalog.Slug, new DateOnly(2026, 11, 10)));
        var trackedOnly = Value(await As(owner).Tracking.ListAsync(catalog.Slug, new TrackingQuery { TrackedOnly = true }));
        var all = Value(await As(owner).Tracking.ListAsync(catalog.Slug, new TrackingQuery()));

        summary.ByStage.Should().Contain(DefaultStage, 1).And.Contain("planning", 1);
        trackedOnly.Items.Select(item => item.Event.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        all.Items.Should().HaveCount(3);
    }

    /// <summary>Creates an owner, a private catalog and one published event in it.</summary>
    private async Task<(EventsActor Owner, string Catalog, EventModel Event)> EventAsync()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        return (owner, catalog.Slug, await CreateEventAsync(owner, catalog.Slug));
    }
}
