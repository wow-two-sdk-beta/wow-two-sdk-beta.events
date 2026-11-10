using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventReviewBroker"/>: the owner organization reviews; only a candidate is accepted,
/// rejected or merged, at the revision read; observations answer 404 outside the catalog's own.</summary>
public abstract class EventReviewBrokerConformance : EventsConformance
{
    /// <summary>A proposal joins the review queue.</summary>
    [Fact]
    public async Task ListAsync_ShouldQueueACandidate()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
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
        var catalog = await CreateCatalogAsync(owner);
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
        var catalog = await CreateCatalogAsync(owner);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);

        var accepted = await As(owner).Review.AcceptAsync(catalog.Slug, candidate.Id, new ReviewDecision { ExpectedRevision = candidate.Revision + 1 });

        Error(accepted).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>A rejected candidate stays rejected: accepting it afterwards conflicts.</summary>
    [Fact]
    public async Task RejectAsync_ShouldRejectTheCandidate_ThenRefuseToAcceptIt()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
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
        var catalog = await CreateCatalogAsync(owner);
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

    /// <summary>Another organization reaches no queue of a private catalog: 404.</summary>
    [Fact]
    public async Task ListAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
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
