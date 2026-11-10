using AwesomeAssertions;
using Events.Tests.Contract;
using Events.Tests.Integration.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Intake;

/// <summary>The contract replay with SCFD (the services sweep): harvest's golden pages pulled in order through the real
/// feed and the real intake over PostgreSQL, each pull sending the cursor the page before it served. One automatic
/// source with the <c>payload</c> classifier takes every page: new, changed, empty, lifecycle, classification, the
/// Afisha place and the purge.</summary>
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
        cursor.Received.Should().Be(16);
        cursor.Quarantined.Should().Be(0);

        var events = read.ServiceProvider.GetRequiredService<ICatalogEventsBroker>();
        var published = (await events.ListAsync(slug, new EventQuery())).ValueOrThrow().Items;
        var candidates = (await events.ListAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).ValueOrThrow().Items;

        // 06: only the relevant, confident classification publishes, with the classifier's kind and name.
        var runway = published.Should().ContainSingle().Subject;
        runway.Facts.Kind.Should().Be("runway");
        runway.ReviewedBy.Should().Be("classifier:model:contract@1");
        candidates.Should().HaveCount(7, "the two jazz posts, three undecided or irrelevant classifications and the two Afisha places wait for a person");

        // 01, 03, 05: one event per post through its revisions, the gone listing, the merge and the unmerge.
        var jazz = candidates.Where(item => item.Facts.Title.Values.Single().StartsWith("Вечер живого джаза", StringComparison.Ordinal)).ToList();
        jazz.Select(item => item.Facts.Title.Values.Single()).Should().BeEquivalentTo(
            ["Вечер живого джаза", "Вечер живого джаза (копия анонса)"], "the unmerge restored each post's own content");
        jazz.Should().OnlyContain(item => item.ReviewedBy == "classifier:payload", "nothing stays held by the source after the listing returned and the unmerge");

        // 07: the Afisha place before the city fix names no city; after it, the city mention gives it.
        candidates.Single(item => item.Facts.Title.Values.Single() == "Вечер камерной музыки").Facts.Place!.City.Should().BeNull();
        candidates.Single(item => item.Facts.Title.Values.Single() == "Концерт молодёжного оркестра").Facts.Place!.City.Should().Be("Ташкент");

        // 08: the purge clears every delivered payload of the post, and keeps its rows.
        var purged = served[0].Records.Single().PostId;
        var rows = await db.Observations.AsNoTracking().Where(row => row.PostId == purged).ToListAsync();
        rows.Should().HaveCount(7).And.OnlyContain(row => row.Payload == null && row.Proposed == null);
        rows.Should().ContainSingle(row => row.Type == "post.purged");
    }

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
