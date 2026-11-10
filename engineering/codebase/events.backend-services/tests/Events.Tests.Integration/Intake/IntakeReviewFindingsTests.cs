using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Events.Tests.Integration.Catalogs;

namespace Events.Tests.Integration.Intake;

/// <summary>The cross-lane review's correctness gaps (2026-11-10), each pinned: a reviewer's candidate keeps its facts, a
/// post's identity stays in its catalog, intake status stays with its owner, a slug alone never routes records, and a
/// purge removes the delivered content.</summary>
public sealed class IntakeReviewFindingsTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    /// <summary>Finding 1: a candidate a person reviewed (reopened, edited) takes a newer revision as a pending change.</summary>
    [Fact]
    public async Task Intake_ShouldKeepAReviewersCandidate_WhenANewerRevisionArrives()
    {
        var (slug, source) = await SourceAsync(auto => auto.Review = "auto");
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Show"));
        await fixture.PollAsync(source);
        var auto = (await EventsAsync(slug, new EventQuery())).Single();
        var reopened = (await Review(review => review.ReopenAsync(slug, auto.Id, auto.Revision))).ValueOrThrow();
        var corrected = reopened.Facts with { Kind = "runway", Place = reopened.Facts.Place! with { City = "Samarkand" } };
        (await Events(events => events.UpdateAsync(slug, auto.Id, new EventSpec { Facts = corrected, ExpectedRevision = reopened.Revision }))).ValueOrThrow();

        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Show, changed", type: "post.changed"));
        await fixture.PollAsync(source);

        var candidate = (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single();
        candidate.Facts.Place!.City.Should().Be("Samarkand");
        candidate.Facts.Title.Values.Single().Should().Be("Show");
        candidate.PendingChanges.Should().Be(1);
    }

    /// <summary>Finding 2: one post delivered into two organizations' catalogs makes one event in each, and neither queue
    /// sees the other's evidence.</summary>
    [Fact]
    public async Task Intake_ShouldKeepOneEventPerCatalog_WhenTwoCatalogsReceiveTheSamePost()
    {
        var post = Guid.NewGuid();
        var (first, firstSource) = await SourceAsync();
        var (second, secondSource) = await SourceAsync();
        fixture.Feed.Append(firstSource, ScfdRecords.Post(post, 1, "Shared post"));
        fixture.Feed.Append(secondSource, ScfdRecords.Post(post, 2, "Shared post, newer"));

        await fixture.PollAsync(firstSource);
        await fixture.PollAsync(secondSource);

        fixture.Principal.ActAsMember(firstSource.OwnerOrg!);
        var firstEvent = (await EventsAsync(first, new EventQuery { Review = EventReviewState.Candidate })).Single();
        firstEvent.Facts.Title.Values.Single().Should().Be("Shared post");
        (await Review(review => review.ListAsync(first, new ReviewQuery()))).ValueOrThrow().Items
            .SelectMany(item => item.Observations).Should().OnlyContain(observation => observation.Catalog == first);

        fixture.Principal.ActAsMember(secondSource.OwnerOrg!);
        (await EventsAsync(second, new EventQuery { Review = EventReviewState.Candidate })).Single()
            .Id.Should().NotBe(firstEvent.Id);
    }

    /// <summary>Finding 3: another organization's admin sees none of a source, its catalog's counts, nor polls it.</summary>
    [Fact]
    public async Task IntakeStatus_ShouldShowNothing_ToAnotherOrganizationsAdmin()
    {
        var (_, source) = await SourceAsync();
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Show"));
        await fixture.PollAsync(source);

        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        await using var scope = fixture.Scope();
        var status = (await scope.ServiceProvider.GetRequiredService<IEventIntakeBroker>().PollAsync()).ValueOrThrow();

        status.Sources.Should().NotContain(item => item.Name == source.Name);
        status.Candidates.Should().Be(0);
        status.PendingChanges.Should().Be(0);
    }

    /// <summary>Finding 4: a source whose catalog another organization owns, or that pins no owner, pulls nothing.</summary>
    [Theory]
    [InlineData("another-org")]
    [InlineData(null)]
    public async Task Intake_ShouldPullNothing_WhenTheCatalogsOwnerIsNotThePinnedOne(string? pinned)
    {
        var (slug, source) = await SourceAsync(options => options.OwnerOrg = pinned);
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Routed by slug alone"));

        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Should().BeEmpty();
        await using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        (await db.IntakeCursors.SingleAsync(row => row.SourceName == source.Name)).LastError.Should().Contain("pulls nothing");
    }

    /// <summary>Finding 5: a purge removes every delivered content of the post: payloads, proposed facts and source fields.</summary>
    [Fact]
    public async Task Intake_ShouldRemoveTheDeliveredContent_WhenThePostIsPurged()
    {
        var (_, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Show", edit: data =>
            data["links"] = new object[] { new { url = "https://afisha.example/x", sourceId = Guid.NewGuid(), sourceFields = new { price = "100000" } } }));
        await fixture.PollAsync(source);

        fixture.Feed.Append(source, ScfdRecords.Raw("post.purged", null, post));
        await fixture.PollAsync(source);

        await using var scope = fixture.Scope();
        var rows = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Observations.AsNoTracking()
            .Where(row => row.PostId == post).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.Payload == null && row.Proposed == null && !row.Links.Contains("sourceFields"));
        rows.Should().Contain(row => row.Links.Contains("https://afisha.example/x"), "the link's reference stays");
    }

    private async Task<(string Slug, EventsIntakeSourceOptions Source)> SourceAsync(Action<EventsIntakeSourceOptions>? configure = null)
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"finding-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        return (slug, fixture.AddSource(slug, configure));
    }

    private async Task<IReadOnlyList<EventModel>> EventsAsync(string slug, EventQuery query) =>
        (await Events(events => events.ListAsync(slug, query))).ValueOrThrow().Items;

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
