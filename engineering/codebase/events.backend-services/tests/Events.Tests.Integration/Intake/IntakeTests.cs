using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Events.Tests.Integration.Catalogs;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Intake;

/// <summary>The SCFD intake (events-integration #15, #19): at least once in, each delivery stored once, the highest
/// revision winning, a page committing whole or not at all, and nothing published unreviewed.</summary>
public sealed class IntakeTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    [Fact]
    public async Task Intake_ShouldStoreEachDeliveryOnce_WhenAPageIsReplayed()
    {
        var (slug, source) = await SourceAsync();
        var first = ScfdRecords.Post(Guid.NewGuid(), 1, "First show");
        var second = ScfdRecords.Post(Guid.NewGuid(), 1, "Second show");
        fixture.Feed.Append(source, first, second, ScfdRecords.Retry(first));

        await fixture.PollAsync(source);
        fixture.Feed.Append(source, ScfdRecords.Retry(first), ScfdRecords.Retry(second));
        await fixture.PollAsync(source);

        var observations = await ObservationsAsync(slug);
        observations.Select(observation => observation.DeliveryEventId).Should().BeEquivalentTo([first.EventId, second.EventId]);
        (await CandidatesAsync(slug)).Should().HaveCount(2);
        var status = await StatusAsync(source);
        status.Received.Should().Be(2);
        status.Cursor.Should().Be("5");
    }

    [Fact]
    public async Task Intake_ShouldKeepTheHighestRevision_WhenRevisionsArriveOutOfOrder()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 3, "Third take", "post.changed"), ScfdRecords.Post(post, 2, "Second take", "post.changed"));
        await fixture.PollAsync(source);
        var afterOutOfOrder = (await CandidatesAsync(slug)).Single();

        fixture.Feed.Append(source, ScfdRecords.Post(post, 4, "Fourth take", "post.changed"), ScfdRecords.Post(post, 4, "Fourth again", "post.changed"));
        await fixture.PollAsync(source);
        var afterNewer = (await CandidatesAsync(slug)).Single();

        afterOutOfOrder.Facts.Title.Values.Should().Equal("Third take");
        afterNewer.Id.Should().Be(afterOutOfOrder.Id);
        afterNewer.Facts.Title.Values.Should().Equal("Fourth take");
        afterNewer.Revision.Should().Be(2);
        var states = (await ObservationsAsync(slug)).Reverse().Select(observation => (observation.Revision, observation.State)).ToList();
        states.Should().Equal(
            ((int?)3, ObservationState.Applied), (2, ObservationState.Superseded), (4, ObservationState.Applied), (4, ObservationState.Superseded));
    }

    [Fact]
    public async Task Intake_ShouldCommitNothing_WhenItCrashesBeforeTheCursorCommit()
    {
        var (slug, source) = await SourceAsync();
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Never committed"));
        fixture.Crash.Arm(source);
        try
        {
            await fixture.PollAsync(source);
        }
        finally
        {
            fixture.Crash.Arm(source, on: false);
        }

        (await ObservationsAsync(slug)).Should().BeEmpty();
        (await CandidatesAsync(slug)).Should().BeEmpty();
        var status = await StatusAsync(source);
        status.Cursor.Should().BeNull();
        status.Received.Should().Be(0);
        status.ConsecutiveFailures.Should().Be(1);
        status.LastError.Should().Contain("Simulated crash");
    }

    [Fact]
    public async Task Intake_ShouldResumeFromTheCommittedCursor_AfterACrash()
    {
        var (slug, source) = await SourceAsync(configure: options => (options.PageSize, options.MaxPagesPerPoll) = (2, 1));
        var records = Enumerable.Range(1, 4).Select(index => ScfdRecords.Post(Guid.NewGuid(), 1, $"Show {index}")).ToArray();
        fixture.Feed.Append(source, records);

        await fixture.PollAsync(source);
        fixture.Crash.Arm(source);
        try
        {
            await fixture.PollAsync(source);
        }
        finally
        {
            fixture.Crash.Arm(source, on: false);
        }

        var afterCrash = await StatusAsync(source);
        await fixture.PollAsync(source);
        var resumed = await StatusAsync(source);

        afterCrash.Cursor.Should().Be("2");
        afterCrash.Received.Should().Be(2);
        resumed.Cursor.Should().Be("4");
        resumed.Received.Should().Be(4);
        resumed.ConsecutiveFailures.Should().Be(0);
        resumed.LastError.Should().BeNull();
        fixture.Feed.Reads.Where(read => read.Source == source.Name).Select(read => read.Cursor).Should().Equal(null, "2", "2");
        (await ObservationsAsync(slug)).Select(observation => observation.DeliveryEventId)
            .Should().BeEquivalentTo(records.Select(record => record.EventId));
    }

    [Fact]
    public async Task Intake_ShouldQuarantineAMalformedRecord_WithoutBlockingThePage()
    {
        var (slug, source) = await SourceAsync();
        var valid = ScfdRecords.Post(Guid.NewGuid(), 1, "Valid show");
        fixture.Feed.Append(source,
            ScfdRecords.Raw("post.new", null, Guid.NewGuid(), 1),
            ScfdRecords.Post(Guid.NewGuid(), 1, "Other schema", edit: data => data["schema"] = "wow2.post.v1"),
            ScfdRecords.Post(Guid.NewGuid(), 1, "No title", edit: data => data.Remove("title")),
            ScfdRecords.Post(Guid.NewGuid(), 1, "Yearless", edit: data => data["starts"] = new { value = "--03-14", precision = "day" }),
            ScfdRecords.Raw("post.new", JsonSerializer.SerializeToElement("not an object"), Guid.NewGuid(), 1),
            valid);

        await fixture.PollAsync(source);

        var observations = await ObservationsAsync(slug);
        observations.Should().HaveCount(6);
        observations.Where(observation => observation.State == ObservationState.Quarantined).Select(observation => observation.QuarantineReason)
            .Should().BeEquivalentTo(["no payload", "schema 'wow2.post.v1' is not wow2.event.v1", "no title",
                "unreadable dates: starts '--03-14' at precision 'day'", "the payload is not an object"]);
        observations.Single(observation => observation.DeliveryEventId == valid.EventId).State.Should().Be(ObservationState.Applied);
        var status = await StatusAsync(source);
        status.Quarantined.Should().Be(5);
        status.Received.Should().Be(6);
        status.Cursor.Should().Be("6");
    }

    [Fact]
    public async Task Intake_ShouldCreateACandidate_AndPublishNothing()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        var record = ScfdRecords.Post(post, 1, "  Humo Fashion Night ");
        fixture.Feed.Append(source, record);

        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty();
        var candidate = (await CandidatesAsync(slug)).Single();
        candidate.Review.Should().Be(EventReviewState.Candidate);
        candidate.PublishedAtUtc.Should().BeNull();
        candidate.Facts.Title.Should().Equal(new Dictionary<string, string> { ["ru-Cyrl"] = "Humo Fashion Night" });
        candidate.Facts.Kind.Should().Be("other");
        candidate.Facts.Starts.Should().Be(new EventWhen { Value = "2026-12-05", Precision = DatePrecision.Day, Certainty = DateCertainty.Announced });
        candidate.Facts.Place!.City.Should().Be("Tashkent");
        candidate.Facts.Place.Venue.Should().Be("Humo Arena");
        candidate.Facts.Organizer!.Name.Should().Be("Mstar");
        candidate.Facts.Website.Should().Be("https://mstar.uz/event");
        candidate.Facts.Offers.Should().ContainSingle().Which.Kind.Should().Be(OfferKind.Ticket);
        candidate.Facts.Images.Should().BeEmpty();
        candidate.Facts.Summary.Should().BeEmpty();
        candidate.Sources.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Kind = EventSourceKind.Scfd, Id = $"scfd/{post:D}" });

        var observation = (await ObservationsAsync(slug)).Single();
        observation.State.Should().Be(ObservationState.Applied);
        observation.EventId.Should().Be(candidate.Id);
        observation.SourceInstance.Should().Be("scfd");
        observation.SubscriptionId.Should().Be(source.SubscriptionId);
        observation.PipelineId.Should().Be(source.PipelineId);
        observation.MappingVersion.Should().Be("wow2.event.v1/norm@2/rules@4/schemaOrg");
        observation.Links.Should().HaveCount(2);
        observation.Links[0].Url.Should().Be($"https://afisha.example/{post:N}");
        observation.Payload!.Value.GetProperty("title").GetString().Should().Be("  Humo Fashion Night ");
    }

    [Fact]
    public async Task Intake_ShouldProposeAChange_WhenAPublishedEventsPostChanges()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Original title"));
        await fixture.PollAsync(source);
        var candidate = (await CandidatesAsync(slug)).Single();
        (await Review(broker => broker.AcceptAsync(slug, candidate.Id, new ReviewDecision { ExpectedRevision = candidate.Revision }))).ValueOrThrow();

        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Renamed by source", "post.changed"));
        await fixture.PollAsync(source);
        fixture.Feed.Append(source, ScfdRecords.Post(post, 3, "Renamed again", "post.changed"));
        await fixture.PollAsync(source);

        var published = (await EventsAsync(slug, new EventQuery())).Single();
        published.Facts.Title.Values.Should().Equal("Original title");
        published.Revision.Should().Be(2);
        published.PendingChanges.Should().Be(1);
        var observations = (await ObservationsAsync(slug)).ToDictionary(observation => observation.Revision!.Value);
        observations[2].State.Should().Be(ObservationState.Superseded);
        observations[3].State.Should().Be(ObservationState.Pending);
        observations[3].Proposed!.Title.Values.Should().Equal("Renamed again");
        observations[3].EventId.Should().Be(published.Id);
    }

    [Fact]
    public async Task Intake_ShouldApplyNothing_WhenTheCatalogIsNotPrivate()
    {
        var (slug, source) = await SourceAsync(CatalogVisibility.Public);
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Would be republished"), ScfdRecords.Raw("pipeline.run", null));

        await fixture.PollAsync(source);

        (await CandidatesAsync(slug)).Should().BeEmpty();
        var observations = await ObservationsAsync(slug);
        observations.Should().HaveCount(2).And.OnlyContain(observation =>
            observation.State == ObservationState.Quarantined && observation.QuarantineReason == "catalog not private" && observation.EventId == null);
        (await StatusAsync(source)).Quarantined.Should().Be(2);
    }

    [Fact]
    public async Task Intake_ShouldFeedOneEvent_WhenTwoSubscriptionsDeliverTheSamePost()
    {
        var (slug, fashion) = await SourceAsync();
        var exhibitions = fixture.AddSource(slug);
        var post = Guid.NewGuid();
        fixture.Feed.Append(fashion, ScfdRecords.Post(post, 1, "Shared show"));
        fixture.Feed.Append(exhibitions, ScfdRecords.Post(post, 2, "Shared show, updated", "post.changed"), ScfdRecords.Post(post, 1, "Shared show"));

        await fixture.PollAsync(fashion);
        await fixture.PollAsync(exhibitions);

        var candidate = (await CandidatesAsync(slug)).Single();
        candidate.Facts.Title.Values.Should().Equal("Shared show, updated");
        candidate.Sources.Should().ContainSingle().Which.Id.Should().Be($"scfd/{post:D}");
        var observations = await ObservationsAsync(slug);
        observations.Select(observation => (observation.SubscriptionId, observation.Revision, observation.State)).Should().BeEquivalentTo(
        [
            (fashion.SubscriptionId, 1, ObservationState.Applied),
            (exhibitions.SubscriptionId, 2, ObservationState.Applied),
            (exhibitions.SubscriptionId, 1, ObservationState.Superseded),
        ]);
        observations.Should().OnlyContain(observation => observation.EventId == candidate.Id);
        (await StatusAsync(fashion)).Cursor.Should().Be("1");
        (await StatusAsync(exhibitions)).Cursor.Should().Be("2");
    }

    [Fact]
    public async Task Intake_ShouldRecordLifecycleRecords_WithoutChangingFacts()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Steady show"));
        await fixture.PollAsync(source);
        var before = (await CandidatesAsync(slug)).Single();

        fixture.Feed.Append(source,
            ScfdRecords.Raw("link.state", JsonSerializer.SerializeToElement(new { state = "gone" }), post),
            ScfdRecords.Raw("post.merged", null, post),
            ScfdRecords.Raw("pipeline.health", JsonSerializer.SerializeToElement(new { health = "blocked" })),
            ScfdRecords.Raw("something.new", null));
        await fixture.PollAsync(source);

        var after = (await CandidatesAsync(slug)).Single();
        after.Revision.Should().Be(before.Revision);
        after.Facts.Should().BeEquivalentTo(before.Facts);
        var recorded = (await ObservationsAsync(slug)).Where(observation => observation.State == ObservationState.Recorded).ToList();
        recorded.Select(observation => (observation.Type, observation.EventId)).Should().BeEquivalentTo(new (string, Guid?)[]
        {
            ("link.state", before.Id), ("post.merged", before.Id), ("pipeline.health", null), ("something.new", null),
        });
    }

    [Fact]
    public async Task Intake_ShouldHonourAPurge_ByClearingThePostsPayloads()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Purged later"));
        await fixture.PollAsync(source);
        var before = (await CandidatesAsync(slug)).Single();

        fixture.Feed.Append(source, ScfdRecords.Raw("post.purged", JsonSerializer.SerializeToElement(new { postId = post }), post));
        await fixture.PollAsync(source);

        var observations = await ObservationsAsync(slug);
        observations.Should().HaveCount(2).And.OnlyContain(observation => observation.Payload == null);
        observations.Single(observation => observation.Type == "post.purged").State.Should().Be(ObservationState.Recorded);
        (await CandidatesAsync(slug)).Single().Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Intake_ShouldRecordAFailedPoll_AndRecoverOnTheNext()
    {
        var (slug, source) = await SourceAsync();
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "After the outage"));
        fixture.Feed.Fail(source, new HttpRequestException("SCFD answered 503.", null, HttpStatusCode.ServiceUnavailable));

        await fixture.PollAsync(source);
        await fixture.PollAsync(source);
        var failing = await StatusAsync(source);
        fixture.Feed.Fail(source, null);
        await fixture.PollAsync(source);
        var recovered = await StatusAsync(source);

        failing.ConsecutiveFailures.Should().Be(2);
        failing.LastError.Should().Be("HttpRequestException: SCFD answered 503.");
        failing.LastPolledAtUtc.Should().Be(fixture.Clock.GetUtcNow());
        recovered.ConsecutiveFailures.Should().Be(0);
        recovered.LastError.Should().BeNull();
        recovered.Received.Should().Be(1);
        recovered.LastReceivedAtUtc.Should().Be(fixture.Clock.GetUtcNow());
        recovered.NewestObservedAtUtc.Should().Be(ScfdRecords.OccurredAt);
        (await CandidatesAsync(slug)).Should().HaveCount(1);
    }

    [Fact]
    public async Task PollAsync_ShouldPollEveryEnabledSource_AndReportTheQueue()
    {
        var (_, enabled) = await SourceAsync();
        var (_, disabled) = await SourceAsync(configure: options => options.Enabled = false);
        fixture.Feed.Append(enabled, ScfdRecords.Post(Guid.NewGuid(), 1, "Polled"));
        fixture.Feed.Append(disabled, ScfdRecords.Post(Guid.NewGuid(), 1, "Not polled"));

        await using var scope = fixture.Scope();
        var status = (await scope.ServiceProvider.GetRequiredService<IEventIntakeBroker>().PollAsync()).ValueOrThrow();

        status.Sources.Single(item => item.Name == enabled.Name).Received.Should().Be(1);
        var off = status.Sources.Single(item => item.Name == disabled.Name);
        off.Enabled.Should().BeFalse();
        off.Received.Should().Be(0);
        off.Cursor.Should().BeNull();
        status.Candidates.Should().BeGreaterThanOrEqualTo(1);
        status.OldestUnresolvedAtUtc.Should().NotBeNull();
    }

    private async Task<(string Slug, EventsIntakeSourceOptions Source)> SourceAsync(
        CatalogVisibility visibility = CatalogVisibility.Private, Action<EventsIntakeSourceOptions>? configure = null)
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"intake-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, visibility, "runway");
        return (slug, fixture.AddSource(slug, configure));
    }

    private async Task<IReadOnlyList<ObservationModel>> ObservationsAsync(string slug) =>
        (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { Limit = 200 }))).ValueOrThrow().Items;

    private async Task<IReadOnlyList<EventModel>> CandidatesAsync(string slug) =>
        await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate });

    private async Task<IReadOnlyList<EventModel>> EventsAsync(string slug, EventQuery query)
    {
        await using var scope = fixture.Scope();
        return (await scope.ServiceProvider.GetRequiredService<ICatalogEventsBroker>().ListAsync(slug, query)).ValueOrThrow().Items;
    }

    private async Task<IntakeSourceStatusModel> StatusAsync(EventsIntakeSourceOptions source)
    {
        await using var scope = fixture.Scope();
        var status = (await scope.ServiceProvider.GetRequiredService<IEventIntakeBroker>().GetStatusAsync()).ValueOrThrow();
        return status.Sources.Single(item => item.Name == source.Name);
    }

    private async Task<T> Review<T>(Func<IEventReviewBroker, Task<T>> call)
    {
        await using var scope = fixture.Scope();
        return await call(scope.ServiceProvider.GetRequiredService<IEventReviewBroker>());
    }
}
