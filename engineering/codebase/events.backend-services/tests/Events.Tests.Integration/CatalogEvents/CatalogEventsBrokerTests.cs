using AwesomeAssertions;
using Events.Tests.Integration.Catalogs;
using Events.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.CatalogEvents;

/// <summary>The module's catalog events: curators' writes, readers' views, and the list's filters.</summary>
public sealed class CatalogEventsBrokerTests(ModuleFixture fixture) : IClassFixture<ModuleFixture>
{
    [Fact]
    public async Task CreateAsync_ShouldPublishTheEvent_WithAManualSource()
    {
        var slug = await OwnCatalogAsync();

        var created = (await Events(broker => broker.CreateAsync(slug, new EventSpec
        {
            Facts = TestCatalogs.Facts("  Tashkent Fashion Week ", "2026-10", DatePrecision.Month) with { Key = "tfw-2026", Tags = ["Fashion", "fashion"] },
        }))).ValueOrThrow();

        created.Review.Should().Be(EventReviewState.Published);
        created.PublishedAtUtc.Should().Be(fixture.Clock.GetUtcNow());
        created.Revision.Should().Be(1);
        created.Facts.Title["und"].Should().Be("Tashkent Fashion Week");
        created.Facts.Tags.Should().Equal("fashion");
        created.Sources.Should().ContainSingle().Which.Kind.Should().Be(EventSourceKind.Manual);
        created.Sources[0].Id.Should().StartWith("person-1/");
        (await Events(broker => broker.GetAsync(slug, created.Id))).ValueOrThrow().Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task CreateAsync_ShouldRefuseATakenKey_AndAKindOutsideTheCatalog()
    {
        var slug = await OwnCatalogAsync();
        await Events(broker => broker.CreateAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("First") with { Key = "same" } }));

        var taken = await Events(broker => broker.CreateAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Second") with { Key = "same" } }));
        var kind = await Events(broker => broker.CreateAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Third", kind: "concert") }));

        TestResults.ErrorOf(taken).Type.Should().Be(AppErrorType.Conflict);
        TestResults.ErrorOf(kind).Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_ShouldRefuse_WhenTheCallerDoesNotOwnTheCatalog()
    {
        var slug = await OwnCatalogAsync(CatalogVisibility.Public);
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());

        var created = await Events(broker => broker.CreateAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Not mine") }));
        var proposed = await Events(broker => broker.ProposeAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Not mine") }));

        TestResults.ErrorOf(created).Type.Should().Be(AppErrorType.Forbidden);
        TestResults.ErrorOf(proposed).Type.Should().Be(AppErrorType.Forbidden);
    }

    [Fact]
    public async Task ProposeAsync_ShouldCreateACandidate_ThatPublishesOnlyOnAccept()
    {
        var slug = await OwnCatalogAsync(CatalogVisibility.Public);
        var owner = fixture.Principal.OrganizationId!;

        var proposed = (await Events(broker => broker.ProposeAsync(slug, new EventSpec
        {
            Facts = TestCatalogs.Facts("Proposed show", "2026-12-01") with { Key = "proposed-show" },
        }))).ValueOrThrow();
        var taken = await Events(broker => broker.ProposeAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Again") with { Key = "proposed-show" } }));
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var unseen = await Events(broker => broker.GetAsync(slug, proposed.Id));
        var readersBefore = (await Events(broker => broker.ListAsync(slug, new EventQuery()))).ValueOrThrow();

        fixture.Principal.ActAsMember(owner);
        var accepted = (await Review(broker => broker.AcceptAsync(slug, proposed.Id, new ReviewDecision { ExpectedRevision = proposed.Revision }))).ValueOrThrow();
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var readersAfter = (await Events(broker => broker.ListAsync(slug, new EventQuery()))).ValueOrThrow();

        proposed.Review.Should().Be(EventReviewState.Candidate);
        proposed.PublishedAtUtc.Should().BeNull();
        proposed.Sources.Should().ContainSingle().Which.Kind.Should().Be(EventSourceKind.Manual);
        TestResults.ErrorOf(taken).Type.Should().Be(AppErrorType.Conflict);
        TestResults.ErrorOf(unseen).Type.Should().Be(AppErrorType.NotFound);
        readersBefore.Items.Should().BeEmpty();
        accepted.Review.Should().Be(EventReviewState.Published);
        accepted.PublishedAtUtc.Should().NotBeNull();
        readersAfter.Items.Should().ContainSingle().Which.Id.Should().Be(proposed.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldBumpTheRevision_AndRefuseAStaleOne()
    {
        var slug = await OwnCatalogAsync();
        var created = (await Events(broker => broker.CreateAsync(slug, new EventSpec
        {
            Facts = TestCatalogs.Facts("Before") with
            {
                Deadlines =
                [
                    new EventDeadline { Kind = DeadlineKind.Application, At = new EventWhen { Value = "2026-11-20", Precision = DatePrecision.Day } },
                    new EventDeadline { Kind = DeadlineKind.Payment, At = new EventWhen { Value = "2026-11-25", Precision = DatePrecision.Day } },
                ],
            },
        }))).ValueOrThrow();

        var missing = await Events(broker => broker.UpdateAsync(slug, created.Id, new EventSpec { Facts = TestCatalogs.Facts("After") }));
        var updated = (await Events(broker => broker.UpdateAsync(slug, created.Id, new EventSpec
        {
            Facts = TestCatalogs.Facts("After") with
            {
                Deadlines = [new EventDeadline { Kind = DeadlineKind.Registration, At = new EventWhen { Value = "2026-12", Precision = DatePrecision.Month } }],
            },
            ExpectedRevision = 1,
        }))).ValueOrThrow();
        var stale = await Events(broker => broker.UpdateAsync(slug, created.Id, new EventSpec { Facts = TestCatalogs.Facts("Late"), ExpectedRevision = 1 }));

        TestResults.ErrorOf(missing).Type.Should().Be(AppErrorType.Validation);
        updated.Revision.Should().Be(2);
        updated.Facts.Title["und"].Should().Be("After");
        updated.Facts.Deadlines.Should().ContainSingle().Which.Kind.Should().Be(DeadlineKind.Registration);
        TestResults.ErrorOf(stale).Type.Should().Be(AppErrorType.Conflict);
    }

    [Fact]
    public async Task ArchiveAsync_ShouldArchiveTheEvent_AndHideItFromReaders()
    {
        var slug = await OwnCatalogAsync();
        var created = (await Events(broker => broker.CreateAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Gone soon") }))).ValueOrThrow();

        var archived = (await Events(broker => broker.ArchiveAsync(slug, created.Id, created.Revision))).ValueOrThrow();
        var again = await Events(broker => broker.ArchiveAsync(slug, created.Id, archived.Revision));
        var published = (await Events(broker => broker.ListAsync(slug, new EventQuery()))).ValueOrThrow();
        var archivedList = (await Events(broker => broker.ListAsync(slug, new EventQuery { Review = EventReviewState.Archived }))).ValueOrThrow();

        archived.Review.Should().Be(EventReviewState.Archived);
        archived.Revision.Should().Be(2);
        TestResults.ErrorOf(again).Type.Should().Be(AppErrorType.Conflict);
        published.Items.Should().BeEmpty();
        archivedList.Items.Should().ContainSingle().Which.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task ListAsync_ShouldMatchMonthSpans_AndListUndatedLast()
    {
        var slug = await OwnCatalogAsync();
        var month = await CreateAsync(slug, TestCatalogs.Facts("October month", "2026-10", DatePrecision.Month));
        await CreateAsync(slug, TestCatalogs.Facts("Mid October", "2026-10-15"));
        var undated = await CreateAsync(slug, TestCatalogs.Facts("Some day"));
        await CreateAsync(slug, TestCatalogs.Facts("December", "2026-12-01"));
        var spanning = await CreateAsync(slug, TestCatalogs.Facts("Long run", "2026-10-10") with
        {
            Ends = new EventWhen { Value = "2026-10-25", Precision = DatePrecision.Day },
        });

        var window = (await Events(broker => broker.ListAsync(slug, new EventQuery
        {
            From = new DateOnly(2026, 10, 20), To = new DateOnly(2026, 10, 31),
        }))).ValueOrThrow();
        var datedOnly = (await Events(broker => broker.ListAsync(slug, new EventQuery
        {
            From = new DateOnly(2026, 10, 20), To = new DateOnly(2026, 10, 31), IncludeUndated = false,
        }))).ValueOrThrow();

        window.Items.Select(item => item.Id).Should().Equal(month.Id, spanning.Id, undated.Id);
        datedOnly.Items.Select(item => item.Id).Should().Equal(month.Id, spanning.Id);
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByKindCountryCityTextAndStatus()
    {
        var slug = await OwnCatalogAsync(kinds: ["runway", "expo"]);
        var target = await CreateAsync(slug, TestCatalogs.Facts("Silk Road Runway", "2026-11-01") with
        {
            Place = new EventPlace { City = "Samarkand", Country = "uz", Venue = "Registan" },
            Organizer = new EventOrganizer { Name = "Silk Guild" },
            Status = EventStatus.Postponed,
        });
        await CreateAsync(slug, TestCatalogs.Facts("Expo Hall", "2026-11-02", kind: "expo") with { Place = new EventPlace { City = "Tashkent", Country = "UZ" } });
        await CreateAsync(slug, TestCatalogs.Facts("Almaty Runway", "2026-11-03") with { Place = new EventPlace { City = "Almaty", Country = "KZ" } });

        async Task<IReadOnlyList<Guid>> Ids(EventQuery query) =>
            (await Events(broker => broker.ListAsync(slug, query))).ValueOrThrow().Items.Select(item => item.Id).ToList();

        (await Ids(new EventQuery { Kind = "RUNWAY", Country = "uz" })).Should().Equal(target.Id);
        (await Ids(new EventQuery { City = "samarkand" })).Should().Equal(target.Id);
        (await Ids(new EventQuery { Text = "silk guild" })).Should().Equal(target.Id);
        (await Ids(new EventQuery { Text = "registan" })).Should().Equal(target.Id);
        (await Ids(new EventQuery { Status = EventStatus.Postponed })).Should().Equal(target.Id);
        (await Ids(new EventQuery { Kind = "expo" })).Should().HaveCount(1).And.NotContain(target.Id);
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByDeadlineBefore_IgnoringPassedDeadlines()
    {
        var slug = await OwnCatalogAsync();
        EventFacts WithDeadline(string title, string at, DatePrecision precision) => TestCatalogs.Facts(title, "2027-01-15") with
        {
            Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = new EventWhen { Value = at, Precision = precision } }],
        };
        var soon = await CreateAsync(slug, WithDeadline("Soon", "2026-11-20", DatePrecision.Day));
        var monthLong = await CreateAsync(slug, WithDeadline("This month", "2026-11", DatePrecision.Month));
        await CreateAsync(slug, WithDeadline("Passed", "2026-11-01", DatePrecision.Day));
        await CreateAsync(slug, WithDeadline("Later", "2026-12-20", DatePrecision.Day));
        await CreateAsync(slug, TestCatalogs.Facts("No deadline", "2027-01-15"));

        var listed = (await Events(broker => broker.ListAsync(slug, new EventQuery { DeadlineBefore = new DateOnly(2026, 11, 30) }))).ValueOrThrow();

        listed.Items.Select(item => item.Id).Should().BeEquivalentTo([soon.Id, monthLong.Id]);
    }

    [Fact]
    public async Task ListAsync_ShouldPageWithACursor_AndRefuseAForeignOne()
    {
        var slug = await OwnCatalogAsync();
        for (var day = 1; day <= 5; day++)
            await CreateAsync(slug, TestCatalogs.Facts($"Day {day}", $"2026-11-0{day}"));

        var first = (await Events(broker => broker.ListAsync(slug, new EventQuery { Limit = 2 }))).ValueOrThrow();
        var second = (await Events(broker => broker.ListAsync(slug, new EventQuery { Limit = 2, Cursor = first.NextCursor }))).ValueOrThrow();
        var third = (await Events(broker => broker.ListAsync(slug, new EventQuery { Limit = 2, Cursor = second.NextCursor }))).ValueOrThrow();
        var foreign = await Events(broker => broker.ListAsync(slug, new EventQuery { Limit = 2, Kind = "runway", Cursor = first.NextCursor }));

        first.Items.Select(item => item.Facts.Title["und"]).Should().Equal("Day 1", "Day 2");
        second.Items.Select(item => item.Facts.Title["und"]).Should().Equal("Day 3", "Day 4");
        third.Items.Select(item => item.Facts.Title["und"]).Should().Equal("Day 5");
        third.NextCursor.Should().BeNull();
        TestResults.ErrorOf(foreign).Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public async Task ListAsync_ShouldShowReadersPublishedEventsOnly()
    {
        var slug = await OwnCatalogAsync(CatalogVisibility.Public);
        var owner = fixture.Principal.OrganizationId!;
        var published = await CreateAsync(slug, TestCatalogs.Facts("Published"));
        var candidate = (await Events(broker => broker.ProposeAsync(slug, new EventSpec { Facts = TestCatalogs.Facts("Candidate") }))).ValueOrThrow();

        var ownerCandidates = (await Events(broker => broker.ListAsync(slug, new EventQuery { Review = EventReviewState.Candidate }))).ValueOrThrow();
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var reader = (await Events(broker => broker.ListAsync(slug, new EventQuery()))).ValueOrThrow();
        var readerCandidates = (await Events(broker => broker.ListAsync(slug, new EventQuery { Review = EventReviewState.Candidate }))).ValueOrThrow();
        fixture.Principal.ActAsMember(owner);

        ownerCandidates.Items.Should().ContainSingle().Which.Id.Should().Be(candidate.Id);
        reader.Items.Should().ContainSingle().Which.Id.Should().Be(published.Id);
        readerCandidates.Items.Should().BeEmpty();
    }

    private async Task<string> OwnCatalogAsync(CatalogVisibility visibility = CatalogVisibility.Private, string[]? kinds = null)
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var slug = ModuleFixture.NewSlug();
        await TestCatalogs.CreateAsync(fixture.Services, slug, visibility, kinds ?? ["runway"]);
        return slug;
    }

    private async Task<EventModel> CreateAsync(string slug, EventFacts facts) =>
        (await Events(broker => broker.CreateAsync(slug, new EventSpec { Facts = facts }))).ValueOrThrow();

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
