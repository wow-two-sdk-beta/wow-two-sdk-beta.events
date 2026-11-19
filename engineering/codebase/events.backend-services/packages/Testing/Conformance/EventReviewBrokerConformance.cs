using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventReviewBroker"/>: the owner organization reviews; only a candidate is accepted,
/// rejected or merged, at the revision read; a source splits off an event that holds another one; observations answer 404
/// outside the catalog's own.</summary>
public abstract class EventReviewBrokerConformance : EventsConformance
{
    /// <summary>A proposal joins the review queue.</summary>
    [Fact]
    public async Task ListAsync_ShouldQueueACandidate()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);
        var published = await CreateEventAsync(owner, catalog.Slug);

        var queue = Value(await As(owner).Review.ListAsync(catalog.Slug, new ReviewQuery()));

        queue.Items.Select(item => item.Event.Id).Should().Contain(candidate.Id).And.NotContain(published.Id);
    }

    /// <summary>Accepting a candidate publishes it, with the reviewer's edits.</summary>
    [Fact]
    public async Task AcceptAsync_ShouldPublishTheCandidate_WithTheEdits()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);

        var accepted = Value(await As(owner).Review.AcceptAsync(catalog.Slug, candidate.Id, new ReviewDecision { ExpectedRevision = candidate.Revision, Facts = Facts("Reviewed title") }));

        accepted.Review.Should().Be(EventReviewState.Published);
        accepted.Facts.Title[EventsConstants.UnknownLocale].Should().Be("Reviewed title");
        accepted.PublishedAtUtc.Should().NotBeNull();
        Value(await As(owner).Review.ListAsync(catalog.Slug, new ReviewQuery())).Items.Select(item => item.Event.Id).Should().NotContain(candidate.Id);
    }

    /// <summary>Only a candidate is accepted: a published event conflicts.</summary>
    [Fact]
    public async Task AcceptAsync_ShouldFailConflict_WhenTheEventIsPublished()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var published = await CreateEventAsync(owner, catalog.Slug);

        var accepted = await As(owner).Review.AcceptAsync(catalog.Slug, published.Id, new ReviewDecision { ExpectedRevision = published.Revision });

        Error(accepted).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>An accept at a stale revision conflicts.</summary>
    [Fact]
    public async Task AcceptAsync_ShouldFailConflict_WhenTheRevisionIsStale()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);

        var accepted = await As(owner).Review.AcceptAsync(catalog.Slug, candidate.Id, new ReviewDecision { ExpectedRevision = candidate.Revision + 1 });

        Error(accepted).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>A rejected candidate stays rejected: accepting it afterwards conflicts.</summary>
    [Fact]
    public async Task RejectAsync_ShouldRejectTheCandidate_ThenRefuseToAcceptIt()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);

        var rejected = Value(await As(owner).Review.RejectAsync(catalog.Slug, candidate.Id, candidate.Revision));
        var accepted = await As(owner).Review.AcceptAsync(catalog.Slug, candidate.Id, new ReviewDecision { ExpectedRevision = rejected.Revision });

        rejected.Review.Should().Be(EventReviewState.Rejected);
        Error(accepted).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>Merging a candidate into a published event keeps the target and archives the candidate into it.</summary>
    [Fact]
    public async Task MergeAsync_ShouldArchiveTheCandidate_IntoThePublishedEvent()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var target = await CreateEventAsync(owner, catalog.Slug);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);

        var merged = Value(await As(owner).Review.MergeAsync(catalog.Slug, candidate.Id, new MergeDecision
        {
            IntoId = target.Id,
            ExpectedRevision = candidate.Revision,
            IntoExpectedRevision = target.Revision,
        }));
        var archived = Value(await As(owner).Events.GetAsync(catalog.Slug, candidate.Id));

        merged.Id.Should().Be(target.Id);
        merged.Review.Should().Be(EventReviewState.Published);
        archived.Review.Should().Be(EventReviewState.Archived);
        archived.MergedInto.Should().Be(target.Id);
    }

    /// <summary>Splitting a source off an event moves it to a new candidate that holds the event's facts, leaves the event its
    /// other source and queues the candidate for review.</summary>
    [Fact]
    public async Task SplitSourceAsync_ShouldMoveTheSourceToANewCandidate_AndLeaveTheEventItsOther()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var (merged, leaving, kept) = await MergedEventAsync(owner, catalog.Slug);

        var split = Value(await As(owner).Review.SplitSourceAsync(catalog.Slug, merged.Id, Decision(leaving, merged.Revision)));
        var left = Value(await As(owner).Events.GetAsync(catalog.Slug, merged.Id));

        split.Id.Should().NotBe(merged.Id);
        split.Review.Should().Be(EventReviewState.Candidate);
        split.Facts.Title.Should().Equal(merged.Facts.Title, "a curator's source reports no facts of its own");
        split.Facts.Key.Should().BeNull("the key stays with the event");
        split.Sources.Should().ContainSingle().Which.Id.Should().Be(leaving.Id);
        left.Sources.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
        left.Review.Should().Be(EventReviewState.Published);
        left.Revision.Should().BeGreaterThan(merged.Revision);
        Value(await As(owner).Review.ListAsync(catalog.Slug, new ReviewQuery())).Items.Select(item => item.Event.Id).Should().Contain(split.Id);
    }

    /// <summary>A split needs another source to stay: an event's only source, or one it does not hold, fails validation.</summary>
    [Fact]
    public async Task SplitSourceAsync_ShouldFailValidation_WhenTheSourceIsTheOnlyOneOrNotTheEvents()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var alone = await CreateEventAsync(owner, catalog.Slug);
        var (merged, leaving, _) = await MergedEventAsync(owner, catalog.Slug);
        var review = As(owner).Review;

        Error(await review.SplitSourceAsync(catalog.Slug, alone.Id, Decision(alone.Sources.Single(), alone.Revision))).Type.Should().Be(AppErrorType.Validation);
        Error(await review.SplitSourceAsync(catalog.Slug, merged.Id, new SourceSplitDecision { Kind = leaving.Kind, SourceId = "nobody/none", ExpectedRevision = merged.Revision }))
            .Type.Should().Be(AppErrorType.Validation);
        Error(await review.SplitSourceAsync(catalog.Slug, merged.Id, new SourceSplitDecision { Kind = EventSourceKind.Import, SourceId = leaving.Id, ExpectedRevision = merged.Revision }))
            .Type.Should().Be(AppErrorType.Validation);
        Value(await As(owner).Events.GetAsync(catalog.Slug, merged.Id)).Sources.Should().HaveCount(2, "a refused split changes nothing");
    }

    /// <summary>A split at a stale revision conflicts, and so does one on an event that is neither a candidate nor published.</summary>
    [Fact]
    public async Task SplitSourceAsync_ShouldFailConflict_WhenTheRevisionIsStale_OrTheEventIsRejected()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var (merged, leaving, _) = await MergedEventAsync(owner, catalog.Slug);
        var rejected = await ProposeEventAsync(owner, catalog.Slug);
        Value(await As(owner).Review.RejectAsync(catalog.Slug, rejected.Id, rejected.Revision));

        Error(await As(owner).Review.SplitSourceAsync(catalog.Slug, merged.Id, Decision(leaving, merged.Revision + 1))).Type.Should().Be(AppErrorType.Conflict);
        Error(await As(owner).Review.SplitSourceAsync(catalog.Slug, rejected.Id, Decision(rejected.Sources.Single(), rejected.Revision + 1))).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>Another organization splits nothing of a private catalog's events: 404; an unknown event is 404 too.</summary>
    [Fact]
    public async Task SplitSourceAsync_ShouldFailNotFound_ForAnotherOrganization_OrAnUnknownEvent()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var (merged, leaving, _) = await MergedEventAsync(owner, catalog.Slug);

        Error(await As(NewMember()).Review.SplitSourceAsync(catalog.Slug, merged.Id, Decision(leaving, merged.Revision))).Type.Should().Be(AppErrorType.NotFound);
        Error(await As(owner).Review.SplitSourceAsync(catalog.Slug, Guid.NewGuid(), Decision(leaving, 1))).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>Builds an event with two sources the way a reviewer's merge does: a curated event and a member's proposal
    /// merged into it. Returns the event, the source that joined (the proposal's) and the one it started from.</summary>
    private async Task<(EventModel Event, EventSourceModel Joined, EventSourceModel Started)> MergedEventAsync(EventsActor owner, string slug)
    {
        var target = await CreateEventAsync(owner, slug, Facts("Shared fair", "2026-12-05", key: $"shared-{Guid.NewGuid():N}"[..30]));
        var candidate = await ProposeEventAsync(owner, slug, Facts("Shared fair twin", "2026-12-05"));
        Value(await As(owner).Review.MergeAsync(slug, candidate.Id, new MergeDecision
        {
            IntoId = target.Id,
            ExpectedRevision = candidate.Revision,
            IntoExpectedRevision = target.Revision,
        }));
        var merged = Value(await As(owner).Events.GetAsync(slug, target.Id));
        return (merged, candidate.Sources.Single(), target.Sources.Single());
    }

    private static SourceSplitDecision Decision(EventSourceModel source, int revision) =>
        new() { Kind = source.Kind, SourceId = source.Id, ExpectedRevision = revision };

    /// <summary>Another organization reaches no queue of a private catalog: 404.</summary>
    [Fact]
    public async Task ListAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);
        var other = As(NewMember());

        Error(await other.Review.ListAsync(catalog.Slug, new ReviewQuery())).Type.Should().Be(AppErrorType.NotFound);
        Error(await other.Review.AcceptAsync(catalog.Slug, candidate.Id, new ReviewDecision { ExpectedRevision = candidate.Revision })).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>The observations list answers for the owner; an unknown observation is not found, to read, apply or dismiss.</summary>
    [Fact]
    public async Task Observations_ShouldListForTheOwner_AndFailNotFound_WhenUnknown()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var unknown = Guid.NewGuid();
        var review = As(owner).Review;

        Value(await review.ListObservationsAsync(catalog.Slug, new ObservationQuery { State = ObservationState.Pending })).Items.Should().BeEmpty();
        Error(await review.GetObservationAsync(catalog.Slug, unknown)).Type.Should().Be(AppErrorType.NotFound);
        Error(await review.ApplyChangeAsync(catalog.Slug, unknown, new ReviewDecision { ExpectedRevision = 1 })).Type.Should().Be(AppErrorType.NotFound);
        Error(await review.DismissChangeAsync(catalog.Slug, unknown)).Type.Should().Be(AppErrorType.NotFound);
    }
}
