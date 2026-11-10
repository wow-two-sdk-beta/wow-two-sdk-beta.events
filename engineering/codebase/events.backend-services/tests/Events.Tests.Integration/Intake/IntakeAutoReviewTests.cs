using System.Text.Json;
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

    /// <summary>The live Afisha event (events-dev, 2026-11-10): its classifier published it from a record that names no
    /// city; SCFD's next revision adds a city mention, and the event takes it with no person's step and nothing pending.
    /// The place parts are the live record's, before and after SCFD's fix, as the scfd lane sent them.</summary>
    [Fact]
    public async Task Intake_ShouldTakeANewRevisionsCity_WhenTheClassifierPublishedTheEvent()
    {
        var (slug, source) = await SourceAsync(auto => { auto.Review = "auto"; auto.DefaultCountry = "UZ"; });
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Показ мод The Fashion Show Line 2026", edit: data => Place(data, LivePlaceBefore)));
        await fixture.PollAsync(source);
        var before = (await EventsAsync(slug, new EventQuery())).Single();

        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Показ мод The Fashion Show Line 2026", type: "post.changed", edit: data => Place(data, LivePlaceAfter)));
        await fixture.PollAsync(source);

        before.Facts.Place!.City.Should().BeNull();
        before.Facts.Place.Address.Should().Be("Ташкент, просп. Мустакиллик, 2");
        var after = (await EventsAsync(slug, new EventQuery())).Single();
        after.Id.Should().Be(before.Id);
        after.Review.Should().Be(EventReviewState.Published);
        after.ReviewedBy.Should().StartWith(IntakeVerdict.ClassifierActor);
        after.PendingChanges.Should().Be(0);
        after.Facts.Place.Should().BeEquivalentTo(new EventPlace
        {
            Venue = "Дворец творчества молодёжи", Address = "просп. Мустакиллик, 2", City = "Ташкент", Country = "UZ", Latitude = 41.3153, Longitude = 69.2727,
        });
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

    /// <summary>The backlog pass pages by a stable order (the rollout's EF warning: paging with no order can skip or repeat
    /// rows), so a backlog larger than one read is checked whole and each held event once; it counts what it re-mapped.</summary>
    [Fact]
    public async Task ReviewBacklog_ShouldCheckEveryHeldEventOnce_AcrossSeveralReads()
    {
        var (slug, source) = await SourceAsync(manual => manual.Filter = "ocharo-fashion-v1");
        for (var index = 0; index < 5; index++)
            fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, $"Show {index}"));
        await fixture.PollAsync(source);
        source.Review = "auto";
        source.DefaultCountry = "UZ";

        var first = await BacklogAsync(source, batch: 2);
        var second = await BacklogAsync(source, batch: 2);

        first.Should().Be(new BacklogPass(Checked: 5, Remapped: 5, Published: 5));
        second.Should().Be(new BacklogPass(Checked: 5, Remapped: 0, Published: 0), "a second pass finds nothing new");
        (await EventsAsync(slug, new EventQuery())).Should().HaveCount(5).And.OnlyContain(item => item.Facts.Place!.Country == "UZ");
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

    private const string LivePlaceBefore = """
        {"place":{"mentions":[{"text":"Дворец творчества молодёжи","kind":"venue","geo":null},{"text":"Ташкент, просп. Мустакиллик, 2","kind":"address","geo":null}],"geo":{"latitude":41.3153,"longitude":69.2727}},
         "schemaOrg":{"@type":"Event","location":{"@type":"Place","name":"Дворец творчества молодёжи","address":"Ташкент, просп. Мустакиллик, 2","geo":{"@type":"GeoCoordinates","latitude":"41.3153000","longitude":"69.2727000"}}}}
        """;

    private const string LivePlaceAfter = """
        {"place":{"mentions":[{"text":"Дворец творчества молодёжи","kind":"venue","geo":null},{"text":"Ташкент","kind":"city","geo":null},{"text":"просп. Мустакиллик, 2","kind":"street","geo":null}],"geo":{"latitude":41.3153,"longitude":69.2727}},
         "schemaOrg":{"@type":"Event","location":{"@type":"Place","name":"Дворец творчества молодёжи","address":{"@type":"PostalAddress","addressLocality":"Ташкент","streetAddress":"просп. Мустакиллик, 2"},"geo":{"@type":"GeoCoordinates","latitude":"41.3153000","longitude":"69.2727000"}}}}
        """;

    private static void Place(Dictionary<string, object?> data, string parts)
    {
        using var document = JsonDocument.Parse(parts);
        data["place"] = document.RootElement.GetProperty("place").Clone();
        data["schemaOrg"] = document.RootElement.GetProperty("schemaOrg").Clone();
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

    private async Task<BacklogPass> BacklogAsync(EventsIntakeSourceOptions source, int batch)
    {
        await using var scope = fixture.Scope();
        return await scope.ServiceProvider.GetRequiredService<IntakeProcessor>().ReviewBacklogAsync(source, CancellationToken.None, batch);
    }

    private async Task<T> Review<T>(Func<IEventReviewBroker, Task<T>> call)
    {
        await using var scope = fixture.Scope();
        return await call(scope.ServiceProvider.GetRequiredService<IEventReviewBroker>());
    }
}
