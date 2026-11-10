using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Events.Tests.Integration.Catalogs;

namespace Events.Tests.Integration.Intake;

/// <summary>An intake source's automatic review (events-integration #53): it publishes what its classifier marks relevant,
/// holds the rest as candidates, records the classifier and its verdict, and gives way to any person's review.</summary>
public sealed class IntakeAutoReviewTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    [Fact]
    public async Task Intake_ShouldPublish_WhenTheSourceReviewsAutomaticallyAndItsFilterPassedTheRecord()
    {
        var (slug, source) = await SourceAsync(auto => { auto.Review = "auto"; auto.Filter = "ocharo-fashion-v1"; });
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Fashion Show Line 2026"));

        await fixture.PollAsync(source);

        var published = (await EventsAsync(slug, new EventQuery())).Single();
        published.Review.Should().Be(EventReviewState.Published);
        published.PublishedAtUtc.Should().NotBeNull();
        published.ReviewedBy.Should().Be("classifier:filter:ocharo-fashion-v1");
        published.ReviewReason.Should().Be("passed SCFD filter ocharo-fashion-v1");
    }

    [Fact]
    public async Task Intake_ShouldHoldACandidate_WhenThePayloadClassifierSaysIrrelevant()
    {
        var (slug, source) = await SourceAsync(auto => { auto.Review = "auto"; auto.Classifier = "payload"; });
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Car wash opening", edit: data =>
            data["classification"] = new { classifier = "relevance-ai@1", verdict = "irrelevant", confidence = 0.92, reason = "not a fashion event" }));

        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty();
        var held = (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single();
        held.ReviewedBy.Should().Be("classifier:relevance-ai@1");
        held.ReviewReason.Should().Be("held: irrelevant (0.92): not a fashion event");
    }

    [Fact]
    public async Task Intake_ShouldHoldACandidate_WhenThePayloadCarriesNoClassification()
    {
        var (slug, source) = await SourceAsync(auto => { auto.Review = "auto"; auto.Classifier = "payload"; });
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Unclassified show"));

        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single()
            .ReviewReason.Should().Be("held: no classification in the record");
    }

    [Fact]
    public async Task Intake_ShouldApplyANewRevision_WhenTheClassifierPublishedTheEvent()
    {
        var (slug, source) = await SourceAsync(auto => auto.Review = "auto");
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Show, first title"));
        await fixture.PollAsync(source);

        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Show, corrected title", type: "post.changed"));
        await fixture.PollAsync(source);

        var published = (await EventsAsync(slug, new EventQuery())).Single();
        published.Facts.Title.Values.Should().ContainSingle().Which.Should().Be("Show, corrected title");
        published.PendingChanges.Should().Be(0);
    }

    [Fact]
    public async Task Intake_ShouldKeepAChangePending_WhenAPersonReviewedTheEventSince()
    {
        var (slug, source) = await SourceAsync(auto => auto.Review = "auto");
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Show, first title"));
        await fixture.PollAsync(source);
        var auto = (await EventsAsync(slug, new EventQuery())).Single();
        var reopened = (await Review(review => review.ReopenAsync(slug, auto.Id, auto.Revision))).ValueOrThrow();
        (await Review(review => review.AcceptAsync(slug, auto.Id, new ReviewDecision { ExpectedRevision = reopened.Revision }))).ValueOrThrow();

        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Show, corrected title", type: "post.changed"));
        await fixture.PollAsync(source);

        var published = (await EventsAsync(slug, new EventQuery())).Single();
        published.Facts.Title.Values.Should().ContainSingle().Which.Should().Be("Show, first title");
        published.PendingChanges.Should().Be(1);
        published.ReviewedBy.Should().StartWith("person:");
    }

    [Fact]
    public async Task ReopenAsync_ShouldReturnAnAutoAcceptedEventToReview()
    {
        var (slug, source) = await SourceAsync(auto => auto.Review = "auto");
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Show"));
        await fixture.PollAsync(source);
        var auto = (await EventsAsync(slug, new EventQuery())).Single();

        var reopened = (await Review(review => review.ReopenAsync(slug, auto.Id, auto.Revision))).ValueOrThrow();

        reopened.Review.Should().Be(EventReviewState.Candidate);
        reopened.ReviewReason.Should().Be("reopened for review");
        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty();
    }

    /// <summary>A candidate stored under manual review (the live Afisha record on dev, 2026-11-10) publishes on the next
    /// poll once its source turns to automatic review, with no new revision.</summary>
    [Fact]
    public async Task Intake_ShouldPublishTheHeldBacklog_WhenTheSourceTurnsAutomatic()
    {
        var (slug, source) = await SourceAsync(manual => manual.Filter = "ocharo-fashion-v1");
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Show stored under manual review"));
        await fixture.PollAsync(source);
        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty();

        source.Review = "auto";
        await fixture.PollAsync(source);

        var published = (await EventsAsync(slug, new EventQuery())).Single();
        published.ReviewedBy.Should().Be("classifier:filter:ocharo-fashion-v1");
        published.ReviewReason.Should().Be("passed SCFD filter ocharo-fashion-v1");
    }

    /// <summary>The backlog pass never overrides a decision: a candidate a person reopened stays in review.</summary>
    [Fact]
    public async Task Intake_ShouldLeaveAReopenedCandidate_WhenItReviewsTheBacklog()
    {
        var (slug, source) = await SourceAsync(auto => auto.Review = "auto");
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Show"));
        await fixture.PollAsync(source);
        var auto = (await EventsAsync(slug, new EventQuery())).Single();
        (await Review(review => review.ReopenAsync(slug, auto.Id, auto.Revision))).ValueOrThrow();

        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty();
    }

    /// <summary>A better reading reaches events the classifier already published, once: the source's default country here
    /// (afisha.uz lists Uzbekistan's events, and its records name no country).</summary>
    [Fact]
    public async Task Intake_ShouldRemapAPublishedEventOnce_WhenTheSourceGainsADefaultCountry()
    {
        var (slug, source) = await SourceAsync(auto => auto.Review = "auto");
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Show"));
        await fixture.PollAsync(source);
        var before = (await EventsAsync(slug, new EventQuery())).Single();
        before.Facts.Place!.Country.Should().BeNull();

        source.DefaultCountry = "UZ";
        await fixture.PollAsync(source);
        await fixture.PollAsync(source);

        var after = (await EventsAsync(slug, new EventQuery())).Single();
        after.Facts.Place!.Country.Should().Be("UZ");
        after.Facts.Place.City.Should().Be("Tashkent");
        after.Revision.Should().Be(before.Revision + 1, "a second poll with nothing new changes nothing");
    }

    [Fact]
    public async Task Intake_ShouldTakeTheClassifiersKind_WhenItIsACatalogKind()
    {
        var (slug, source) = await SourceAsync(auto => { auto.Review = "auto"; auto.Classifier = "payload"; });
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Показ мод", edit: data =>
            data["classification"] = new { classifier = "relevance-ai@1", verdict = "relevant", kind = "runway" }));

        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Single().Facts.Kind.Should().Be("runway");
    }

    [Fact]
    public async Task Intake_ShouldKeepCandidates_WhenTheSourceReviewsManually()
    {
        var (slug, source) = await SourceAsync();
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Show"));

        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty();
        (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single().ReviewedBy.Should().BeNull();
    }

    private async Task<(string Slug, EventsIntakeSourceOptions Source)> SourceAsync(Action<EventsIntakeSourceOptions>? configure = null)
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"auto-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        return (slug, fixture.AddSource(slug, configure));
    }

    private async Task<IReadOnlyList<EventModel>> EventsAsync(string slug, EventQuery query)
    {
        await using var scope = fixture.Scope();
        return (await scope.ServiceProvider.GetRequiredService<ICatalogEventsBroker>().ListAsync(slug, query)).ValueOrThrow().Items;
    }

    private async Task<T> Review<T>(Func<IEventReviewBroker, Task<T>> call)
    {
        await using var scope = fixture.Scope();
        return await call(scope.ServiceProvider.GetRequiredService<IEventReviewBroker>());
    }
}
