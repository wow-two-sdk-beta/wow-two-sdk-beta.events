using AwesomeAssertions;
using Events.Tests.Integration.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Intake;

/// <summary>Cross-source duplicates (events-integration E17): a post whose title, dates and city match an event another
/// source reported joins that event instead of starting a second one; the first source's facts stay; a reviewer splits a
/// wrong match and the pair never attaches again.</summary>
public sealed class IntakeCrossSourceTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    private const string Rule = "rule:title-date-city";
    private const string Afisha = "Afisha Uzbekistan";
    private const string Uzexpo = "Uzexpocentre";

    /// <summary>The spec's done-when: two sources deliver "TITF 2026" on overlapping dates in Tashkent, and one event holds both.</summary>
    [Fact]
    public async Task Intake_ShouldKeepOneEventWithTwoSources_WhenTwoSourcesDeliverTheSameFair()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(afisha, Fair(first, 1, "TITF 2026", name: Afisha));
        fixture.Feed.Append(uzexpo, Fair(second, 1, "TITF, 2026", starts: "2026-12-07", ends: "2026-12-10", name: Uzexpo));

        await fixture.PollAsync(afisha);
        var started = (await AllEventsAsync(slug)).Single();
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await fixture.PollAsync(uzexpo);

        var event1 = (await AllEventsAsync(slug)).Should().ContainSingle("one fair is one event").Subject;
        event1.Id.Should().Be(started.Id);
        event1.Sources.Select(source => source.Name).Should().Equal(Afisha, Uzexpo);
        event1.Sources.Select(source => source.MatchedBy).Should().Equal(new string?[] { null, Rule });
        event1.Sources.Select(source => source.Id).Should().Equal($"scfd/{first:D}", $"scfd/{second:D}");
        event1.Facts.Title.Values.Should().ContainSingle("the first source's facts stay").Which.Should().Be("TITF 2026");
        event1.Facts.Starts.Value.Should().Be("2026-12-05");
        event1.Review.Should().Be(EventReviewState.Published);
        event1.Revision.Should().BeGreaterThan(started.Revision, "a new source is a change a reviewer's read must see");
        var joined = (await ObservationsAsync(slug)).Single(observation => observation.PostId == second);
        joined.EventId.Should().Be(event1.Id);
        joined.State.Should().Be(ObservationState.Applied);
    }

    /// <summary>One selector subscription delivers every source's posts: two sources of one page join one event, the first
    /// record leading it.</summary>
    [Fact]
    public async Task Intake_ShouldJoinOneEvent_WhenOneSubscriptionDeliversBothSourcesInOnePage()
    {
        var (slug, selector, _) = await TwoSourcesAsync();
        var (first, second, third) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(
            selector,
            Fair(first, 1, "TITF 2026", name: "ANONS"),
            Fair(second, 1, "TITF 2026", name: Uzexpo),
            Fair(third, 1, "TITF 2026", name: "CAEx", city: "Samarkand"));

        await fixture.PollAsync(selector);

        var events = await AllEventsAsync(slug);
        events.Should().HaveCount(2);
        var fair = events.Single(item => item.Sources.Count == 2);
        fair.Sources.Select(source => source.Name).Should().Equal("ANONS", Uzexpo);
        fair.Sources.Select(source => source.MatchedBy).Should().Equal(new string?[] { null, Rule });
        events.Single(item => item != fair).Sources.Should().ContainSingle().Which.Name.Should().Be("CAEx");
    }

    [Fact]
    public async Task Intake_ShouldKeepTwoEvents_WhenTheCityDiffers()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        fixture.Feed.Append(afisha, Fair(Guid.NewGuid(), 1, "TITF 2026", name: Afisha));
        fixture.Feed.Append(uzexpo, Fair(Guid.NewGuid(), 1, "TITF 2026", city: "Samarkand", name: Uzexpo));

        await fixture.PollAsync(afisha);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await fixture.PollAsync(uzexpo);

        var events = await AllEventsAsync(slug);
        events.Should().HaveCount(2);
        events.Should().OnlyContain(item => item.Sources.Count == 1 && item.Sources[0].MatchedBy == null);
    }

    /// <summary>The rule needs every part: a title that matches, dates that overlap and the same city, or none for both.</summary>
    [Theory]
    [InlineData("Tashkent International Trade Fair", "Tashkent", "Tashkent International Trade Fair 2026", "2026-12-05", "2026-12-08", "Tashkent", 1)]
    [InlineData("Tashkent International Trade Fair", "Tashkent", "“Tashkent International Trade Fair”", "2026-12-08", "2026-12-09", "Toshkent", 1)]
    [InlineData("Tashkent International Trade Fair", "Tashkent", "Tashkent International Trade Fair Autumn", "2026-12-05", "2026-12-08", "Tashkent", 1)]
    [InlineData("Tashkent International Trade Fair", "Tashkent", "Tashkent International Trade", "2026-12-05", "2026-12-08", "Tashkent", 2)]
    [InlineData("Tashkent International Trade Fair", "Tashkent", "Tashkent International Trade Fair", "2026-12-09", "2026-12-11", "Tashkent", 2)]
    [InlineData("Tashkent International Trade Fair", "Tashkent", "Tashkent Fashion Week", "2026-12-05", "2026-12-08", "Tashkent", 2)]
    [InlineData("Tashkent International Trade Fair", "Tashkent", "Tashkent International Trade Fair", "2026-12-05", "2026-12-08", null, 2)]
    [InlineData("Tashkent International Trade Fair", null, "Tashkent International Trade Fair", "2026-12-05", "2026-12-08", null, 1)]
    [InlineData("Tashkent International Trade Fair", "Tashkent", "Tashkent International Trade Fair", null, null, "Tashkent", 2)]
    public async Task Intake_ShouldJoinTheEvent_OnlyWhenTitleDatesAndCityMatch(
        string firstTitle, string? firstCity, string secondTitle, string? secondStarts, string? secondEnds, string? secondCity, int expected)
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        fixture.Feed.Append(afisha, Fair(Guid.NewGuid(), 1, firstTitle, city: firstCity, name: Afisha));
        fixture.Feed.Append(uzexpo, Fair(Guid.NewGuid(), 1, secondTitle, city: secondCity, starts: secondStarts, ends: secondEnds, name: Uzexpo));

        await fixture.PollAsync(afisha);
        await fixture.PollAsync(uzexpo);

        (await AllEventsAsync(slug)).Should().HaveCount(expected);
    }

    /// <summary>Only a candidate or published event of the catalog is a match: a rejected event takes no source, and another
    /// catalog's events are never reached.</summary>
    [Fact]
    public async Task Intake_ShouldStartAnEvent_WhenTheMatchingEventIsRejectedOrInAnotherCatalog()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        fixture.Feed.Append(afisha, Fair(Guid.NewGuid(), 1, "TITF 2026", name: Afisha));
        await fixture.PollAsync(afisha);
        var published = (await AllEventsAsync(slug)).Single();
        var reopened = (await Review(review => review.ReopenAsync(slug, published.Id, published.Revision))).ValueOrThrow();
        (await Review(review => review.RejectAsync(slug, published.Id, reopened.Revision))).ValueOrThrow();
        var (otherSlug, other) = await SourceInNewCatalogAsync();
        fixture.Feed.Append(other, Fair(Guid.NewGuid(), 1, "TITF 2026", name: Uzexpo));
        await fixture.PollAsync(other);
        fixture.Feed.Append(uzexpo, Fair(Guid.NewGuid(), 1, "TITF 2026", name: Uzexpo));

        await fixture.PollAsync(uzexpo);

        (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Rejected })).Should().ContainSingle();
        (await AllEventsAsync(slug)).Should().ContainSingle().Which.Sources.Should().ContainSingle("the rejected event took no second source");
        (await AllEventsAsync(otherSlug)).Should().ContainSingle().Which.Sources.Should().ContainSingle("a catalog's events never join another's");
    }

    /// <summary>"The first source's facts stay" under automatic review: a later revision of the joined source is evidence
    /// only, while the source the event started from keeps driving the facts.</summary>
    [Fact]
    public async Task Intake_ShouldKeepTheFirstSourcesFacts_WhenTheJoinedSourceChanges()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(afisha, Fair(first, 1, "TITF 2026", name: Afisha));
        fixture.Feed.Append(uzexpo, Fair(second, 1, "TITF 2026", name: Uzexpo));
        await fixture.PollAsync(afisha);
        await fixture.PollAsync(uzexpo);

        fixture.Feed.Append(uzexpo, Fair(second, 2, "TITF 2026 International", type: "post.changed", name: Uzexpo));
        await fixture.PollAsync(uzexpo);

        var event1 = (await AllEventsAsync(slug)).Single();
        event1.Facts.Title.Values.Should().ContainSingle("the joined source's revision never rewrites the facts").Which.Should().Be("TITF 2026");
        event1.PendingChanges.Should().Be(0);
        (await ReviewQueueAsync(slug)).Should().BeEmpty("nothing waits for a reviewer");
        (await ObservationsAsync(slug)).Where(observation => observation.PostId == second).Select(observation => observation.State)
            .Should().BeEquivalentTo([ObservationState.Applied, ObservationState.Applied]);

        fixture.Feed.Append(afisha, Fair(first, 2, "TITF 2026 Tashkent", type: "post.changed", name: Afisha));
        await fixture.PollAsync(afisha);

        (await AllEventsAsync(slug)).Single().Facts.Title.Values.Should().ContainSingle("the first source still follows its own revisions").Which.Should().Be("TITF 2026 Tashkent");
    }

    /// <summary>Visa Fashion Week exists curated and has a twin on another source: the twin joins the curated event, which
    /// keeps its facts and its person's review, and no pending change reaches the queue.</summary>
    [Fact]
    public async Task Intake_ShouldJoinACuratedEvent_AndLeaveItsFactsAndReviewAlone()
    {
        var (slug, _, anons) = await TwoSourcesAsync();
        var curated = await CreateEventAsync(slug, TestCatalogs.Facts("Visa Fashion Week", "2026-12-05") with
        {
            Place = new EventPlace { City = "Tashkent" },
            Ends = new EventWhen { Value = "2026-12-07", Precision = DatePrecision.Day },
        });
        var twin = Guid.NewGuid();
        fixture.Feed.Append(anons, Fair(twin, 1, "Visa Fashion Week 2026", starts: "2026-12-06", ends: "2026-12-07", name: "ANONS"));

        await fixture.PollAsync(anons);

        var event1 = (await AllEventsAsync(slug)).Should().ContainSingle("the twin joined the curated event").Subject;
        event1.Id.Should().Be(curated.Id);
        event1.Facts.Should().BeEquivalentTo(curated.Facts);
        event1.ReviewedBy.Should().Be(curated.ReviewedBy);
        event1.Sources.Select(source => source.Kind).Should().Equal(EventSourceKind.Manual, EventSourceKind.Scfd);
        event1.Sources[1].MatchedBy.Should().Be(Rule);
        event1.Sources[1].Name.Should().Be("ANONS");

        fixture.Feed.Append(anons, Fair(twin, 2, "Visa Fashion Week 2026 Tashkent", starts: "2026-12-06", ends: "2026-12-07", type: "post.changed", name: "ANONS"));
        await fixture.PollAsync(anons);

        (await ReviewQueueAsync(slug)).Should().BeEmpty("a twin's revision proposes no change to a reviewed event");
        (await AllEventsAsync(slug)).Single().Facts.Should().BeEquivalentTo(curated.Facts);
    }

    /// <summary>SCFD reports one source's listing: while a twin still lists the event, one source's end holds nothing, and
    /// the event waits for a person only when every source has ended.</summary>
    [Fact]
    public async Task Intake_ShouldKeepTheEventPublished_WhileASourceStillListsIt()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(afisha, Fair(first, 1, "TITF 2026", name: Afisha));
        fixture.Feed.Append(uzexpo, Fair(second, 1, "TITF 2026", name: Uzexpo));
        await fixture.PollAsync(afisha);
        await fixture.PollAsync(uzexpo);

        fixture.Feed.Append(uzexpo, Fair(second, 1, "TITF 2026", type: "post.unmatched", name: Uzexpo));
        await fixture.PollAsync(uzexpo);

        var kept = (await AllEventsAsync(slug)).Single();
        kept.Review.Should().Be(EventReviewState.Published, "Afisha still lists the fair");
        kept.Sources.Select(source => source.State).Should().Equal(EventSourceState.Live, EventSourceState.Unmatched);

        fixture.Feed.Append(afisha, Fair(first, 1, "TITF 2026", type: "post.unmatched", name: Afisha));
        await fixture.PollAsync(afisha);

        var held = (await AllEventsAsync(slug)).Single();
        held.Review.Should().Be(EventReviewState.Candidate, "no source lists it any more");
        held.ReviewedBy.Should().Be($"{IntakeProcessor.SourceActor}{afisha.Instance}");
        held.ReviewReason.Should().Be(IntakeProcessor.UnmatchedReason);
    }

    /// <summary>The spec's done-when: a split restores two events, and a later delivery does not re-attach.</summary>
    [Fact]
    public async Task SplitSource_ShouldRestoreTwoEvents_AndALaterDeliveryShouldNotReattach()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(afisha, Fair(first, 1, "Tashkent International Trade Fair", name: Afisha));
        fixture.Feed.Append(uzexpo, Fair(second, 1, "Tashkent International Trade Fair Autumn", starts: "2026-12-07", ends: "2026-12-10", name: Uzexpo));
        await fixture.PollAsync(afisha);
        await fixture.PollAsync(uzexpo);
        var joined = (await AllEventsAsync(slug)).Single();
        var leaving = joined.Sources.Single(source => source.MatchedBy == Rule);

        var split = (await Review(review => review.SplitSourceAsync(slug, joined.Id, new SourceSplitDecision
        {
            Kind = leaving.Kind,
            SourceId = leaving.Id,
            ExpectedRevision = joined.Revision,
        }))).ValueOrThrow();

        split.Id.Should().NotBe(joined.Id);
        split.Review.Should().Be(EventReviewState.Candidate);
        split.Facts.Title.Values.Should().ContainSingle("the new candidate holds what that source reported").Which.Should().Be("Tashkent International Trade Fair Autumn");
        split.Facts.Starts.Value.Should().Be("2026-12-07");
        split.Sources.Should().ContainSingle().Which.Should().BeEquivalentTo(leaving with { MatchedBy = null });
        var restored = (await AllEventsAsync(slug)).Should().HaveCount(2).And.Subject.Single(item => item.Id == joined.Id);
        restored.Sources.Should().ContainSingle().Which.Id.Should().Be($"scfd/{first:D}");
        restored.Revision.Should().BeGreaterThan(joined.Revision);
        restored.Facts.Should().BeEquivalentTo(joined.Facts);
        (await ObservationsAsync(slug)).Where(observation => observation.PostId == second).Should().OnlyContain(observation => observation.EventId == split.Id);

        fixture.Feed.Append(uzexpo, Fair(second, 2, "Tashkent International Trade Fair Autumn", starts: "2026-12-07", ends: "2026-12-10", type: "post.changed", name: Uzexpo));
        await fixture.PollAsync(uzexpo);
        fixture.Feed.Append(afisha, Fair(first, 2, "Tashkent International Trade Fair", type: "post.changed", name: Afisha));
        await fixture.PollAsync(afisha);

        var after = await AllEventsAsync(slug);
        after.Should().HaveCount(2, "the split pair stays two events");
        after.Single(item => item.Id == joined.Id).Sources.Should().ContainSingle().Which.Id.Should().Be($"scfd/{first:D}");
        after.Single(item => item.Id == split.Id).Sources.Should().ContainSingle().Which.Id.Should().Be($"scfd/{second:D}");
    }

    /// <summary>The keep-apart pair is the rule's own memory: a split source never matches the event it left, though the
    /// event is the better match, while another source still does.</summary>
    [Fact]
    public async Task SplitSource_ShouldKeepThePairApart_WhenTheSourceWouldMatchTheEventAgain()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(afisha, Fair(first, 1, "TITF 2026", name: Afisha));
        fixture.Feed.Append(uzexpo, Fair(second, 1, "TITF 2026", name: Uzexpo));
        await fixture.PollAsync(afisha);
        await fixture.PollAsync(uzexpo);
        var joined = (await AllEventsAsync(slug)).Single();
        var split = (await Review(review => review.SplitSourceAsync(slug, joined.Id, new SourceSplitDecision
        {
            Kind = EventSourceKind.Scfd,
            SourceId = $"scfd/{second:D}",
            ExpectedRevision = joined.Revision,
        }))).ValueOrThrow();
        var facts = split.Facts;

        await using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var catalog = await db.Catalogs.SingleAsync(row => row.Slug == slug);
        var again = await CrossSourceMatcher.FindAsync(db, catalog.Id, "scfd", $"scfd/{second:D}", facts, CancellationToken.None);
        var another = await CrossSourceMatcher.FindAsync(db, catalog.Id, "scfd", $"scfd/{Guid.NewGuid():D}", facts, CancellationToken.None);

        (again?.Id).Should().Be(split.Id, "the split source matches its own candidate, never the event it left");
        (another?.Id).Should().Be(joined.Id, "another source's twin still matches the published event");
        (await db.KeepApart.SingleAsync(row => row.CatalogId == catalog.Id)).Should().Match<EventKeepApartEntity>(
            row => row.EventId == joined.Id && row.SourceKind == "scfd" && row.SourceId == $"scfd/{second:D}");
    }

    /// <summary>When the source an event started from leaves, the first source still on it speaks for the event.</summary>
    [Fact]
    public async Task SplitSource_ShouldHandTheEventToTheNextSource_WhenTheFirstOneLeaves()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(afisha, Fair(first, 1, "TITF 2026", name: Afisha));
        fixture.Feed.Append(uzexpo, Fair(second, 1, "TITF 2026", name: Uzexpo));
        await fixture.PollAsync(afisha);
        await fixture.PollAsync(uzexpo);
        var joined = (await AllEventsAsync(slug)).Single();

        var split = (await Review(review => review.SplitSourceAsync(slug, joined.Id, new SourceSplitDecision
        {
            Kind = EventSourceKind.Scfd,
            SourceId = $"scfd/{first:D}",
            ExpectedRevision = joined.Revision,
        }))).ValueOrThrow();
        fixture.Feed.Append(uzexpo, Fair(second, 2, "TITF 2026 Tashkent", type: "post.changed", name: Uzexpo));
        await fixture.PollAsync(uzexpo);

        var remaining = (await AllEventsAsync(slug)).Single(item => item.Id == joined.Id);
        remaining.Sources.Should().ContainSingle().Which.MatchedBy.Should().BeNull("it speaks for the event now");
        remaining.Facts.Title.Values.Should().ContainSingle().Which.Should().Be("TITF 2026 Tashkent");
        split.Sources.Should().ContainSingle().Which.Id.Should().Be($"scfd/{first:D}");
    }

    [Fact]
    public async Task SplitSource_ShouldFail_WhenTheSourceIsTheOnlyOneOrUnknownOrTheRevisionIsStale()
    {
        var (slug, afisha, uzexpo) = await TwoSourcesAsync();
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(afisha, Fair(first, 1, "TITF 2026", name: Afisha));
        await fixture.PollAsync(afisha);
        var alone = (await AllEventsAsync(slug)).Single();

        var onlySource = await Review(review => review.SplitSourceAsync(slug, alone.Id, Decision($"scfd/{first:D}", alone.Revision)));
        fixture.Feed.Append(uzexpo, Fair(second, 1, "TITF 2026", name: Uzexpo));
        await fixture.PollAsync(uzexpo);
        var joined = (await AllEventsAsync(slug)).Single();
        var unknown = await Review(review => review.SplitSourceAsync(slug, joined.Id, Decision($"scfd/{Guid.NewGuid():D}", joined.Revision)));
        var stale = await Review(review => review.SplitSourceAsync(slug, joined.Id, Decision($"scfd/{second:D}", joined.Revision - 1)));
        var import = await Review(review => review.SplitSourceAsync(slug, joined.Id, new SourceSplitDecision
        {
            Kind = EventSourceKind.Import,
            SourceId = "legacy/1",
            ExpectedRevision = joined.Revision,
        }));

        ((Result<EventModel>.Failure)onlySource).Error.Type.Should().Be(AppErrorType.Validation);
        ((Result<EventModel>.Failure)unknown).Error.Type.Should().Be(AppErrorType.Validation);
        ((Result<EventModel>.Failure)stale).Error.Type.Should().Be(AppErrorType.Conflict);
        ((Result<EventModel>.Failure)import).Error.Type.Should().Be(AppErrorType.Validation);
        (await AllEventsAsync(slug)).Should().ContainSingle("a refused split changes nothing");
    }

    private static SourceSplitDecision Decision(string sourceId, int revision) =>
        new() { Kind = EventSourceKind.Scfd, SourceId = sourceId, ExpectedRevision = revision };

    /// <summary>Builds a fair's delivery: dates and city as a source states them, and the source's name in the payload's
    /// context, as a selector subscription's source ref carries it.</summary>
    private static ScfdDeliveryRecord Fair(
        Guid post, int revision, string title, string? city = "Tashkent", string? starts = "2026-12-05", string? ends = "2026-12-08",
        string type = "post.new", string name = Afisha) =>
        ScfdRecords.Post(post, revision, title, type, data =>
        {
            data["starts"] = starts is null ? null : new { value = starts, precision = "day" };
            data["ends"] = ends is null ? null : new { value = ends, precision = "day" };
            data["place"] = city is null ? null : new { mentions = new object[] { new { text = city, kind = 0 } } };
            data["context"] = new { sourceName = name };
        });

    /// <summary>A private catalog with two sources that review automatically: each publishes what its filter passed.</summary>
    private async Task<(string Slug, EventsIntakeSourceOptions First, EventsIntakeSourceOptions Second)> TwoSourcesAsync()
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"cross-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        return (slug, fixture.AddSource(slug, Auto), fixture.AddSource(slug, Auto));
    }

    private async Task<(string Slug, EventsIntakeSourceOptions Source)> SourceInNewCatalogAsync()
    {
        var slug = $"cross-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        return (slug, fixture.AddSource(slug, Auto));
    }

    private static void Auto(EventsIntakeSourceOptions source) => source.Review = EventsIntakeSourceOptions.AutoReview;

    private async Task<EventModel> CreateEventAsync(string slug, EventFacts facts)
    {
        await using var scope = fixture.Scope();
        return (await scope.ServiceProvider.GetRequiredService<ICatalogEventsBroker>().CreateAsync(slug, new EventSpec { Facts = facts })).ValueOrThrow();
    }

    /// <summary>Lists every event a reviewer sees, whatever its review state: published and candidates.</summary>
    private async Task<IReadOnlyList<EventModel>> AllEventsAsync(string slug) =>
        (await EventsAsync(slug, new EventQuery())).Concat(await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).ToList();

    private async Task<IReadOnlyList<EventModel>> EventsAsync(string slug, EventQuery query)
    {
        await using var scope = fixture.Scope();
        return (await scope.ServiceProvider.GetRequiredService<ICatalogEventsBroker>().ListAsync(slug, query)).ValueOrThrow().Items;
    }

    private async Task<IReadOnlyList<ObservationModel>> ObservationsAsync(string slug) =>
        (await Review(broker => broker.ListObservationsAsync(slug, new ObservationQuery { Limit = 200 }))).ValueOrThrow().Items;

    private async Task<IReadOnlyList<ReviewItemModel>> ReviewQueueAsync(string slug) =>
        (await Review(broker => broker.ListAsync(slug, new ReviewQuery()))).ValueOrThrow().Items;

    private async Task<T> Review<T>(Func<IEventReviewBroker, Task<T>> call)
    {
        await using var scope = fixture.Scope();
        return await call(scope.ServiceProvider.GetRequiredService<IEventReviewBroker>());
    }
}
