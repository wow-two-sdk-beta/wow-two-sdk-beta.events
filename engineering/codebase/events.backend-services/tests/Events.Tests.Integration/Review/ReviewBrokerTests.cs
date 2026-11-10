using AwesomeAssertions;
using Events.Tests.Integration.Catalogs;
using Events.Tests.Integration.Intake;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Review;

/// <summary>The module's review: the queue, candidate decisions, merges, and pending source changes.</summary>
public sealed class ReviewBrokerTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    [Fact]
    public async Task ListAsync_ShouldQueueCandidatesFirst_ThenPublishedEventsWithPendingChanges()
    {
        var (slug, source) = await SourceAsync();
        var changed = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(changed, 1, "Will change"));
        await fixture.PollAsync(source);
        var toPublish = (await CandidatesAsync(slug)).Single();
        await Review(broker => broker.AcceptAsync(slug, toPublish.Id, new ReviewDecision { ExpectedRevision = toPublish.Revision }));
        fixture.Feed.Append(source, ScfdRecords.Post(changed, 2, "Changed", "post.changed"), ScfdRecords.Post(Guid.NewGuid(), 1, "Fresh candidate"));
        await fixture.PollAsync(source);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var proposed = (await Events(broker => broker.ProposeAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Member's proposal") }))).ValueOrThrow();

        var first = (await Review(broker => broker.ListAsync(slug, new ReviewQuery { Limit = 2 }))).ValueOrThrow();
        var rest = (await Review(broker => broker.ListAsync(slug, new ReviewQuery { Limit = 2, Cursor = first.NextCursor }))).ValueOrThrow();

        first.Items.Select(item => item.Event.Review).Should().Equal(EventReviewState.Candidate, EventReviewState.Candidate);
        first.Items[0].Event.Facts.Title.Values.Should().Equal("Fresh candidate");
        first.Items[0].Observations.Should().ContainSingle().Which.State.Should().Be(ObservationState.Applied);
        first.Items[1].Event.Id.Should().Be(proposed.Id);
        first.Items[1].Observations.Should().BeEmpty();
        rest.Items.Should().ContainSingle();
        rest.Items[0].Event.Id.Should().Be(toPublish.Id);
        rest.Items[0].Observations.Should().ContainSingle().Which.Proposed!.Title.Values.Should().Equal("Changed");
        rest.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task AcceptAsync_ShouldPublishTheCandidate_WithTheReviewersEdits()
    {
        var (slug, source) = await SourceAsync();
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "raw title"));
        await fixture.PollAsync(source);
        var candidate = (await CandidatesAsync(slug)).Single();

        var stale = await Review(broker => broker.AcceptAsync(slug, candidate.Id, new ReviewDecision { ExpectedRevision = 9 }));
        var invalid = await Review(broker => broker.AcceptAsync(slug, candidate.Id, new ReviewDecision
        {
            ExpectedRevision = candidate.Revision, Facts = candidate.Facts with { Kind = "concert" },
        }));
        var accepted = (await Review(broker => broker.AcceptAsync(slug, candidate.Id, new ReviewDecision
        {
            ExpectedRevision = candidate.Revision,
            Facts = candidate.Facts with { Key = "edited", Kind = "runway", Title = new Dictionary<string, string> { ["en"] = "Edited title" } },
        }))).ValueOrThrow();
        var again = await Review(broker => broker.AcceptAsync(slug, candidate.Id, new ReviewDecision { ExpectedRevision = accepted.Revision }));

        TestResults.ErrorOf(stale).Type.Should().Be(AppErrorType.Conflict);
        TestResults.ErrorOf(invalid).Type.Should().Be(AppErrorType.Validation);
        accepted.Review.Should().Be(EventReviewState.Published);
        accepted.PublishedAtUtc.Should().Be(fixture.Clock.GetUtcNow());
        accepted.Revision.Should().Be(candidate.Revision + 1);
        accepted.Facts.Title.Should().Equal(new Dictionary<string, string> { ["en"] = "Edited title" });
        accepted.Facts.Key.Should().Be("edited");
        TestResults.ErrorOf(again).Message.Should().Be(WoW.Two.Sdk.Beta.Be.Events.EventsErrors.ReviewStateInvalid("a candidate").Message);
    }

    [Fact]
    public async Task RejectAsync_ShouldRejectTheCandidate_AndIgnoreItsLaterRevisions()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Not for us"));
        await fixture.PollAsync(source);
        var candidate = (await CandidatesAsync(slug)).Single();

        var rejected = (await Review(broker => broker.RejectAsync(slug, candidate.Id, candidate.Revision))).ValueOrThrow();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Still not for us", "post.changed"));
        await fixture.PollAsync(source);

        rejected.Review.Should().Be(EventReviewState.Rejected);
        (await CandidatesAsync(slug)).Should().BeEmpty();
        var later = (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { State = ObservationState.Ignored }))).ValueOrThrow();
        later.Items.Should().ContainSingle().Which.EventId.Should().Be(candidate.Id);
        var rejectedNow = (await Events(broker => broker.ListAsync(slug, new EventQuery { Review = EventReviewState.Rejected }))).ValueOrThrow();
        rejectedNow.Items.Single().Facts.Title.Values.Should().Equal("Not for us");
    }

    [Fact]
    public async Task MergeAsync_ShouldMoveSourcesAndObservations_AndArchiveTheCandidate()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        var target = (await Events(broker => broker.CreateAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Curated show") }))).ValueOrThrow();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Same show, scraped"));
        await fixture.PollAsync(source);
        var candidate = (await CandidatesAsync(slug)).Single();

        var stale = await Review(broker => broker.MergeAsync(slug, candidate.Id, new MergeDecision
        {
            IntoId = target.Id, ExpectedRevision = candidate.Revision, IntoExpectedRevision = target.Revision + 1,
        }));
        var merged = (await Review(broker => broker.MergeAsync(slug, candidate.Id, new MergeDecision
        {
            IntoId = target.Id, ExpectedRevision = candidate.Revision, IntoExpectedRevision = target.Revision,
        }))).ValueOrThrow();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Same show, renamed", "post.changed"));
        await fixture.PollAsync(source);

        TestResults.ErrorOf(stale).Type.Should().Be(AppErrorType.Conflict);
        merged.Id.Should().Be(target.Id);
        merged.Revision.Should().Be(target.Revision + 1);
        merged.Facts.Title.Values.Should().Equal("Curated show");
        merged.Sources.Select(item => item.Kind).Should().BeEquivalentTo([EventSourceKind.Manual, EventSourceKind.Scfd]);
        var archived = (await Events(broker => broker.GetAsync(slug, candidate.Id))).ValueOrThrow();
        archived.Review.Should().Be(EventReviewState.Archived);
        archived.MergedInto.Should().Be(target.Id);
        archived.Sources.Should().BeEmpty();
        var evidence = (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { EventId = target.Id }))).ValueOrThrow();
        evidence.Items.Select(item => (item.Revision, item.State)).Should().Equal(((int?)2, ObservationState.Pending), (1, ObservationState.Applied));
        (await Events(broker => broker.GetAsync(slug, target.Id))).ValueOrThrow().PendingChanges.Should().Be(1);
    }

    [Fact]
    public async Task ApplyChangeAsync_ShouldApplyTheProposedFacts_AndAcceptTheObservation()
    {
        var (slug, source, published, post) = await PublishedFromSourceAsync();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Renamed by source", "post.changed", data => data["starts"] = new { value = "2027-01", precision = "month" }));
        await fixture.PollAsync(source);
        var pending = (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { State = ObservationState.Pending }))).ValueOrThrow().Items.Single();

        var stale = await Review(broker => broker.ApplyChangeAsync(slug, pending.Id, new ReviewDecision { ExpectedRevision = published.Revision + 5 }));
        var applied = (await Review(broker => broker.ApplyChangeAsync(slug, pending.Id, new ReviewDecision { ExpectedRevision = published.Revision }))).ValueOrThrow();
        var twice = await Review(broker => broker.ApplyChangeAsync(slug, pending.Id, new ReviewDecision { ExpectedRevision = applied.Revision }));
        var observation = (await Review(broker => broker.GetObservationAsync(slug, pending.Id))).ValueOrThrow();

        TestResults.ErrorOf(stale).Type.Should().Be(AppErrorType.Conflict);
        applied.Facts.Title.Values.Should().Equal("Renamed by source");
        applied.Facts.Starts.Should().Be(new EventWhen { Value = "2027-01", Precision = DatePrecision.Month });
        applied.Facts.Key.Should().Be("reviewed-key");
        applied.Facts.Kind.Should().Be("runway");
        applied.Revision.Should().Be(published.Revision + 1);
        applied.PendingChanges.Should().Be(0);
        TestResults.ErrorOf(twice).Type.Should().Be(AppErrorType.Conflict);
        observation.State.Should().Be(ObservationState.Accepted);
    }

    [Fact]
    public async Task DismissChangeAsync_ShouldDismissThePendingChange_AndKeepTheEvent()
    {
        var (slug, source, published, post) = await PublishedFromSourceAsync();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Unwanted rename", "post.changed"));
        await fixture.PollAsync(source);
        var pending = (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { State = ObservationState.Pending }))).ValueOrThrow().Items.Single();

        var dismissed = (await Review(broker => broker.DismissChangeAsync(slug, pending.Id))).ValueOrThrow();
        var again = await Review(broker => broker.DismissChangeAsync(slug, pending.Id));
        var unchanged = (await Events(broker => broker.GetAsync(slug, published.Id))).ValueOrThrow();

        dismissed.State.Should().Be(ObservationState.Dismissed);
        TestResults.ErrorOf(again).Type.Should().Be(AppErrorType.Conflict);
        unchanged.Facts.Title.Values.Should().Equal("Reviewed title");
        unchanged.Revision.Should().Be(published.Revision);
        unchanged.PendingChanges.Should().Be(0);
    }

    [Fact]
    public async Task ListObservationsAsync_ShouldListNewestFirst_AndFilterByState()
    {
        var (slug, source) = await SourceAsync();
        fixture.Feed.Append(source,
            ScfdRecords.Post(Guid.NewGuid(), 1, "One"),
            ScfdRecords.Raw("pipeline.run", null),
            ScfdRecords.Post(Guid.NewGuid(), 1, "Three"));
        await fixture.PollAsync(source);

        var all = (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { Limit = 2 }))).ValueOrThrow();
        var next = (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { Limit = 2, Cursor = all.NextCursor }))).ValueOrThrow();
        var recorded = (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { State = ObservationState.Recorded }))).ValueOrThrow();

        all.Items.Select(item => item.Type).Should().Equal("post.new", "pipeline.run");
        all.Items[0].Proposed!.Title.Values.Should().Equal("Three");
        next.Items.Should().ContainSingle().Which.Proposed!.Title.Values.Should().Equal("One");
        recorded.Items.Should().ContainSingle().Which.Type.Should().Be("pipeline.run");
    }

    [Fact]
    public async Task ReviewCalls_ShouldRefuseAnotherOrganization()
    {
        var (slug, _) = await SourceAsync(CatalogVisibility.Public);
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));

        var queue = await Review(broker => broker.ListAsync(slug, new ReviewQuery()));
        var observations = await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery()));
        var missing = await Review(broker => broker.ListAsync($"missing-{Guid.NewGuid():N}", new ReviewQuery()));

        TestResults.ErrorOf(queue).Type.Should().Be(AppErrorType.Forbidden);
        TestResults.ErrorOf(observations).Type.Should().Be(AppErrorType.Forbidden);
        TestResults.ErrorOf(missing).Type.Should().Be(AppErrorType.NotFound);
    }

    private async Task<(string Slug, EventsIntakeSourceOptions Source, EventModel Published, Guid Post)> PublishedFromSourceAsync()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Scraped title"));
        await fixture.PollAsync(source);
        var candidate = (await CandidatesAsync(slug)).Single();
        var published = (await Review(broker => broker.AcceptAsync(slug, candidate.Id, new ReviewDecision
        {
            ExpectedRevision = candidate.Revision,
            Facts = candidate.Facts with { Key = "reviewed-key", Kind = "runway", Title = new Dictionary<string, string> { ["ru"] = "Reviewed title" } },
        }))).ValueOrThrow();
        return (slug, source, published, post);
    }

    private async Task<(string Slug, EventsIntakeSourceOptions Source)> SourceAsync(CatalogVisibility visibility = CatalogVisibility.Private)
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"review-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, visibility, "runway");
        return (slug, fixture.AddSource(slug));
    }

    private async Task<IReadOnlyList<EventModel>> CandidatesAsync(string slug) =>
        (await Events(broker => broker.ListAsync(slug, new EventQuery { Review = EventReviewState.Candidate }))).ValueOrThrow().Items;

    private async Task<T> Events<T>(Func<ICatalogEventsBroker, Task<T>> call)
    {
        await using var scope = fixture.Scope();
        return await call(scope.ServiceProvider.GetRequiredService<ICatalogEventsBroker>());
    }

    private async Task<T> Review<T>(Func<IEventReviewBroker, Task<T>> call)
    {
        await using var scope = fixture.Scope();
        return await call(scope.ServiceProvider.GetRequiredService<IEventReviewBroker>());
    }
}
