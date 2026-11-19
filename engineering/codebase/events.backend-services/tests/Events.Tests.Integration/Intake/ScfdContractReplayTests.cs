using AwesomeAssertions;
using Events.Tests.Contract;
using Events.Tests.Integration.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Intake;

/// <summary>The contract replay with SCFD (the services sweep): harvest's golden pages pulled in order through the real
/// feed and the real intake over PostgreSQL, each pull sending the cursor the page before it served. One automatic
/// source with the <c>payload</c> classifier takes every page: new, changed, empty, lifecycle, classification, the
/// Afisha place, the purge, the application deadline, the unmatched post and the estimated date.</summary>
public sealed class ScfdContractReplayTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    [Fact]
    public async Task Replay_ShouldStoreEveryPage_FollowItsLifecycle_AndHonourThePurge()
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"contract-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        var source = ContractPages.Source(auto =>
        {
            auto.Name = $"contract-{Guid.NewGuid():N}";
            auto.Catalog = slug;
            auto.Review = EventsIntakeSourceOptions.AutoReview;
            auto.Classifier = EventsIntakeSourceOptions.PayloadClassifier;
            auto.MinConfidence = 0.5;
            auto.DefaultTimeZone = "Asia/Tashkent";
            auto.OwnerOrg = fixture.Principal.OrganizationId;
            auto.OwnerIssuer = fixture.Principal.Issuer;
        });
        var pages = ContractPages.Pages();
        var (feed, scfd) = ContractPages.Feed([.. pages.Select(page => page.Name)]);

        var served = new List<ObservationFeedPage>();
        foreach (var page in pages)
        {
            await using var scope = fixture.Scope();
            var processor = new IntakeProcessor(scope.ServiceProvider.GetRequiredService<EventsDbContext>(), new Recording(feed, served), fixture.Clock, []);
            var outcome = (await processor.ProcessPageAsync(source, CancellationToken.None)).ValueOrThrow();
            await processor.ReviewBacklogAsync(source, CancellationToken.None);

            outcome.Should().Be(new IntakePageOutcome(page.Cases is ["empty"] ? 0 : page.Cases.Count, Quarantined: 0, HasMore: false), page.Name);
        }

        // The cursor contract: each pull sends exactly the cursor the page before it served; an empty page keeps it.
        scfd.Cursors.Should().Equal([null, .. served.SkipLast(1).Select(page => page.Next)]);
        await using var read = fixture.Scope();
        var db = read.ServiceProvider.GetRequiredService<EventsDbContext>();
        var cursor = await db.IntakeCursors.AsNoTracking().SingleAsync(row => row.SourceName == source.Name);
        cursor.Cursor.Should().Be(served[^1].Next);
        cursor.Received.Should().Be(26);
        cursor.Quarantined.Should().Be(0);

        var events = read.ServiceProvider.GetRequiredService<ICatalogEventsBroker>();
        var published = (await events.ListAsync(slug, new EventQuery())).ValueOrThrow().Items;
        var candidates = (await events.ListAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).ValueOrThrow().Items;

        // 06: only the relevant, confident classification publishes, with the classifier's kind and name; 11's estimated post
        // publishes too (below).
        published.Should().HaveCount(2);
        var runway = published.Single(item => SourceTitle(item) != "Tashkent Fashion Week");
        runway.Facts.Kind.Should().Be("runway");
        runway.ReviewedBy.Should().Be("classifier:model:contract@1");
        candidates.Should().HaveCount(10,
            "the two designer-show posts, three undecided or irrelevant classifications, the two Afisha places, the craft fair, the unmatched market and the textile expo wait for a person");

        // 01, 03, 05: one event per post through its revisions, the gone listing, the merge and the unmerge.
        var shows = candidates.Where(item => SourceTitle(item).StartsWith("Показ коллекций молодых дизайнеров", StringComparison.Ordinal)).ToList();
        shows.Select(item => SourceTitle(item)).Should().BeEquivalentTo(
            ["Показ коллекций молодых дизайнеров", "Показ коллекций молодых дизайнеров (копия анонса)"], "the unmerge restored each post's own content");
        shows.Should().OnlyContain(item => item.ReviewedBy == "classifier:payload", "nothing stays held by the source after the listing returned and the unmerge");
        shows.Should().OnlyContain(item => item.Facts.TimeZone == "Asia/Tashkent", "each start states +05:00, Tashkent's offset (E08)");

        // 07: the Afisha place before the city fix names no city; after it, the city mention gives it.
        candidates.Single(item => SourceTitle(item) == "Показ мод выпускников колледжа").Facts.Place!.City.Should().BeNull();
        candidates.Single(item => SourceTitle(item) == "Ярмарка вышивки и текстиля").Facts.Place!.City.Should().Be("Tashkent");

        // 09: the stated application deadline becomes the event's one deadline (E08).
        candidates.Single(item => SourceTitle(item) == "Craft Fair 2026").Facts.Deadlines.Should().Equal(new EventDeadline
        {
            Kind = DeadlineKind.Application,
            At = new EventWhen { Value = "2026-10-26T23:59", Precision = DatePrecision.Minute, Certainty = DateCertainty.Announced },
        });

        // 10: the post that stopped matching holds its event for a person, with the content it matched with (E04).
        var unmatched = candidates.Single(item => SourceTitle(item) == "Маркет ремесленников и дизайнеров");
        unmatched.ReviewedBy.Should().Be($"{IntakeProcessor.SourceActor}{source.Instance}");
        unmatched.ReviewReason.Should().Be(IntakeProcessor.UnmatchedReason);

        // 11: the AI-estimated month reaches the stored observation as an estimate, never as an announced date; the later
        // capture that states the day replaces it, and the event ends on the stated day (S17).
        var estimatedPost = served[pages.ToList().FindIndex(page => page.Name == "11-estimated.json")].Records[0].PostId;
        var proposed = (await db.Observations.AsNoTracking().Where(row => row.PostId == estimatedPost).OrderBy(row => row.Revision).ToListAsync())
            .Select(row => EventsJson.Read<EventFacts>(row.Proposed!).Starts).ToList();
        proposed.Should().Equal(
            new EventWhen(),
            new EventWhen(),
            new EventWhen { Value = "2026-12", Precision = DatePrecision.Month, Certainty = DateCertainty.Estimated },
            new EventWhen { Value = "2026-12-12", Precision = DatePrecision.Day, Certainty = DateCertainty.Announced });
        published.Single(item => SourceTitle(item) == "Tashkent Fashion Week").Facts.Starts.Should().Be(
            new EventWhen { Value = "2026-12-12", Precision = DatePrecision.Day, Certainty = DateCertainty.Announced }, "the stated day replaced the estimate");

        published.Single(item => SourceTitle(item) == "Tashkent Fashion Week").Facts.Title.Should().Equal(
            new Dictionary<string, string> { ["en"] = "Tashkent Fashion Week", ["ru-Cyrl"] = "Tashkent Fashion Week" },
            "harvest v0.2.30 sends the tagging pass's English title beside the source's (OE06)");

        // 12: SCFD's machine English title reaches the event beside the source's Russian one, and the latest Russian title
        // replaces the first (OE06).
        var textile = candidates.Single(item => SourceTitle(item).Contains("швейного оборудования", StringComparison.Ordinal));
        textile.Facts.Title.Should().Equal(new Dictionary<string, string>
        {
            ["en"] = "Textile and Sewing Equipment Exhibition", ["ru-Cyrl"] = "Международная выставка текстиля и швейного оборудования",
        });

        // 08: the purge clears every delivered payload of the post, and keeps its rows.
        var purged = served[0].Records.Single().PostId;
        var rows = await db.Observations.AsNoTracking().Where(row => row.PostId == purged).ToListAsync();
        rows.Should().HaveCount(7).And.OnlyContain(row => row.Payload == null && row.Proposed == null);
        rows.Should().ContainSingle(row => row.Type == "post.purged");
    }

    /// <summary>Reads an event's source title: its one title, or the one beside SCFD's machine <c>en</c> (OE06).</summary>
    private static string SourceTitle(EventModel item) =>
        item.Facts.Title.OrderBy(pair => pair.Key == "en" && item.Facts.Title.Count > 1 ? 1 : 0).First().Value;

    /// <summary>Keeps each page the real feed read, so the test compares the cursors sent with the cursors served.</summary>
    private sealed class Recording(IObservationFeed feed, List<ObservationFeedPage> served) : IObservationFeed
    {
        public async Task<ObservationFeedPage> ReadAsync(EventsIntakeSourceOptions source, string? cursor, int limit, CancellationToken cancellationToken)
        {
            var page = await feed.ReadAsync(source, cursor, limit, cancellationToken);
            served.Add(page);
            return page;
        }

        public Task<ScfdSubscription> ReadSubscriptionAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken) =>
            feed.ReadSubscriptionAsync(source, cancellationToken);
    }
}
