using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventTrackingBroker"/>: tracking belongs to the caller's active organization —
/// shared by its members, invisible to every other organization — moves through the organization's workflow at the
/// revision read, and records each stage change.</summary>
public abstract class EventTrackingBrokerConformance : EventsConformance
{
    /// <summary>Holds the default workflow's first stage.</summary>
    private const string DefaultStage = "interested";

    /// <summary>An event nobody tracks reads with no tracking.</summary>
    [Fact]
    public async Task GetAsync_ShouldReturnTheEvent_WithNoTracking_BeforeAnyPut()
    {
        var (owner, catalog, target) = await EventAsync();

        var tracked = Value(await As(owner).Tracking.GetAsync(catalog, target.Id));

        tracked.Event.Id.Should().Be(target.Id);
        tracked.Tracking.Should().BeNull();
    }

    /// <summary>A put without a stage starts in the workflow's default stage, at revision 1.</summary>
    [Fact]
    public async Task PutAsync_ShouldStartInTheDefaultStage()
    {
        var (owner, catalog, target) = await EventAsync();

        var tracking = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Priority = TrackingPriority.High, Notes = "Apply early" }));

        tracking.EventId.Should().Be(target.Id);
        tracking.Stage.Should().Be(DefaultStage);
        tracking.Priority.Should().Be(TrackingPriority.High);
        tracking.Notes.Should().Be("Apply early");
        tracking.Revision.Should().Be(1);
        Value(await As(owner).Tracking.GetAsync(catalog, target.Id)).Tracking!.Id.Should().Be(tracking.Id);
    }

    /// <summary>A put at a stale revision conflicts.</summary>
    [Fact]
    public async Task PutAsync_ShouldFailConflict_WhenTheRevisionIsStale()
    {
        var (owner, catalog, target) = await EventAsync();
        var first = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec()));
        Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Stage = "planning", ExpectedRevision = first.Revision }));

        var stale = await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Stage = "going", ExpectedRevision = first.Revision });

        Error(stale).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>A stage outside the organization's workflow is invalid.</summary>
    [Fact]
    public async Task PutAsync_ShouldFailValidation_WhenTheStageIsUnknown()
    {
        var (owner, catalog, target) = await EventAsync();

        Error(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Stage = "no-such-stage" })).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>Every member of the organization sees the same tracking.</summary>
    [Fact]
    public async Task Tracking_ShouldBeSharedByTheOrganizationsMembers()
    {
        var (owner, catalog, target) = await EventAsync();
        var tracking = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Notes = "Shared" }));

        var colleague = Value(await As(NewMember(owner.OrganizationId)).Tracking.GetAsync(catalog, target.Id));

        colleague.Tracking!.Id.Should().Be(tracking.Id);
        colleague.Tracking.Notes.Should().Be("Shared");
    }

    /// <summary>Another organization never sees the owner's tracking, even of an event it reads in a public catalog; its
    /// own tracking leaves the owner's untouched.</summary>
    [Fact]
    public async Task Tracking_ShouldStayInsideTheOrganization()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner, CatalogVisibility.Public);
        var target = await CreateEventAsync(owner, catalog.Slug);
        var tracking = Value(await As(owner).Tracking.PutAsync(catalog.Slug, target.Id, new TrackingSpec { Notes = "Owner's notes" }));
        var other = As(NewMember());

        var seen = await other.Tracking.GetAsync(catalog.Slug, target.Id);

        if (seen is Result<TrackedEventModel>.Success { Value: var read })
        {
            read.Tracking.Should().BeNull("another organization's tracking never answers");
            Value(await other.Tracking.PutAsync(catalog.Slug, target.Id, new TrackingSpec { Notes = "Theirs" })).Id.Should().NotBe(tracking.Id);
        }
        else
        {
            Error(seen).Type.Should().Be(AppErrorType.NotFound);
        }

        Value(await As(owner).Tracking.GetAsync(catalog.Slug, target.Id)).Tracking!.Should().BeEquivalentTo(tracking);
    }

    /// <summary>Another organization reaches no tracking of a private catalog: 404.</summary>
    [Fact]
    public async Task GetAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var (_, catalog, target) = await EventAsync();

        Error(await As(NewMember()).Tracking.GetAsync(catalog, target.Id)).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>A caller without an organization, such as a service acting as itself, tracks nothing.</summary>
    [Fact]
    public async Task ListAsync_ShouldFailForbidden_WithoutAnOrganization()
    {
        var (_, catalog, _) = await EventAsync();

        Error(await As(EventsActor.Service()).Tracking.ListAsync(catalog, new TrackingQuery())).Type.Should().Be(AppErrorType.Forbidden);
    }

    /// <summary>A delete at the revision read removes the tracking; a second one finds none.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheTracking_ThenFailNotFound()
    {
        var (owner, catalog, target) = await EventAsync();
        var tracking = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec()));

        Value(await As(owner).Tracking.DeleteAsync(catalog, target.Id, tracking.Revision)).Id.Should().Be(tracking.Id);
        Error(await As(owner).Tracking.DeleteAsync(catalog, target.Id, tracking.Revision)).Type.Should().Be(AppErrorType.NotFound);
        Value(await As(owner).Tracking.GetAsync(catalog, target.Id)).Tracking.Should().BeNull();
    }

    /// <summary>Each stage change lands in the history with its actor.</summary>
    [Fact]
    public async Task GetHistoryAsync_ShouldRecordEachStageChange()
    {
        var (owner, catalog, target) = await EventAsync();
        var first = Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec()));
        Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec { Stage = "planning", ExpectedRevision = first.Revision }));

        var history = Value(await As(owner).Tracking.GetHistoryAsync(catalog, target.Id));

        history.Select(entry => entry.ToStage).Should().Equal(DefaultStage, "planning");
        history[^1].FromStage.Should().Be(DefaultStage);
        history[^1].ActorSubject.Should().Be(owner.Subject);
    }

    /// <summary>The summary counts the organization's tracking per stage, and lists tracked lists only with the filter.</summary>
    [Fact]
    public async Task GetSummaryAsync_ShouldCountTheTrackingPerStage()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var first = await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12-01"));
        var second = await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12-02"));
        await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2026-12-03"));
        Value(await As(owner).Tracking.PutAsync(catalog.Slug, first.Id, new TrackingSpec()));
        Value(await As(owner).Tracking.PutAsync(catalog.Slug, second.Id, new TrackingSpec { Stage = "planning" }));

        var summary = Value(await As(owner).Tracking.GetSummaryAsync(catalog.Slug, new DateOnly(2026, 11, 10)));
        var trackedOnly = Value(await As(owner).Tracking.ListAsync(catalog.Slug, new TrackingQuery { TrackedOnly = true }));
        var all = Value(await As(owner).Tracking.ListAsync(catalog.Slug, new TrackingQuery()));

        summary.ByStage.Should().Contain(DefaultStage, 1).And.Contain("planning", 1);
        trackedOnly.Items.Select(item => item.Event.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        all.Items.Should().HaveCount(3);
    }

    /// <summary>Country lists (E21): <c>Countries</c> lists the events in any of them, <c>ExcludeCountries</c> leaves those out,
    /// and an event with no country matches a country filter only with <c>IncludeUnknownCountry</c>; with no country filter
    /// every event lists.</summary>
    [Fact]
    public async Task ListAsync_ShouldFilterByCountryLists_AndLetUnknownCountriesInOnlyWhenAsked()
    {
        var owner = NewMember();
        var catalog = (await CreateCatalogAsync(owner)).Slug;
        var local = await CreateEventAsync(owner, catalog, InCountry("UZ"));
        var regional = await CreateEventAsync(owner, catalog, InCountry("KZ"));
        var abroad = await CreateEventAsync(owner, catalog, InCountry("FR"));
        var unknown = await CreateEventAsync(owner, catalog, InCountry(null));

        async Task<IEnumerable<Guid>> List(TrackingQuery query) =>
            Value(await As(owner).Tracking.ListAsync(catalog, query)).Items.Select(item => item.Event.Id);

        (await List(new TrackingQuery())).Should().BeEquivalentTo([local.Id, regional.Id, abroad.Id, unknown.Id]);
        (await List(new TrackingQuery { Countries = ["uz", "KZ"] })).Should().BeEquivalentTo([local.Id, regional.Id]);
        (await List(new TrackingQuery { Country = "FR", Countries = ["UZ"] })).Should().BeEquivalentTo([local.Id, abroad.Id], "the single country adds to the list");
        (await List(new TrackingQuery { Countries = ["UZ"], IncludeUnknownCountry = true })).Should().BeEquivalentTo([local.Id, unknown.Id]);
        (await List(new TrackingQuery { ExcludeCountries = ["UZ", "KZ"] })).Should().BeEquivalentTo([abroad.Id], "an event with no country matches no chip");
        (await List(new TrackingQuery { ExcludeCountries = ["UZ", "KZ"], IncludeUnknownCountry = true })).Should().BeEquivalentTo([abroad.Id, unknown.Id]);
        (await List(new TrackingQuery { Countries = ["UZ", "KZ"], ExcludeCountries = ["KZ"] })).Should().BeEquivalentTo([local.Id]);
    }

    /// <summary>A country code that is not two letters is invalid.</summary>
    [Fact]
    public async Task ListAsync_ShouldFailValidation_ForAMalformedCountryCode()
    {
        var (owner, catalog, _) = await EventAsync();

        Error(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { Countries = ["UZB"] })).Type.Should().Be(AppErrorType.Validation);
        Error(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { ExcludeCountries = ["1A"] })).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>The summary counts the published events the organization does not track (E21), apart from another
    /// organization's tracking.</summary>
    [Fact]
    public async Task GetSummaryAsync_ShouldCountTheUntrackedEvents()
    {
        var owner = NewMember();
        var catalog = (await CreateCatalogAsync(owner, CatalogVisibility.Public)).Slug;
        var first = await CreateEventAsync(owner, catalog);
        var second = await CreateEventAsync(owner, catalog);
        await CreateEventAsync(owner, catalog);
        Value(await As(owner).Tracking.PutAsync(catalog, first.Id, new TrackingSpec()));
        var other = As(NewMember());
        var before = Value(await As(owner).Tracking.GetSummaryAsync(catalog, new DateOnly(2026, 11, 10)));
        if (await other.Tracking.PutAsync(catalog, second.Id, new TrackingSpec()) is Result<TrackingModel>.Success)
            Value(await other.Tracking.GetSummaryAsync(catalog, new DateOnly(2026, 11, 10))).Untracked.Should().Be(2, "another organization tracks one");

        var summary = Value(await As(owner).Tracking.GetSummaryAsync(catalog, new DateOnly(2026, 11, 10)));

        before.Untracked.Should().Be(2);
        summary.Untracked.Should().Be(2, "another organization's tracking leaves the owner's count alone");
    }

    /// <summary>Stage lists (PG02): an untracked event reads as the workflow's default stage for <c>Stages</c> and
    /// <c>ExcludeStages</c>, so leaving out a lost stage keeps the untracked events.</summary>
    [Fact]
    public async Task ListAsync_ShouldReadAnUntrackedEventAsTheDefaultStage()
    {
        var owner = NewMember();
        var catalog = (await CreateCatalogAsync(owner)).Slug;
        var planning = await CreateEventAsync(owner, catalog, Facts(starts: "2026-12-01"));
        var skipped = await CreateEventAsync(owner, catalog, Facts(starts: "2026-12-02"));
        var untracked = await CreateEventAsync(owner, catalog, Facts(starts: "2026-12-03"));
        Value(await As(owner).Tracking.PutAsync(catalog, planning.Id, new TrackingSpec { Stage = "planning" }));
        Value(await As(owner).Tracking.PutAsync(catalog, skipped.Id, new TrackingSpec { Stage = "skipped" }));

        async Task<IEnumerable<Guid>> List(TrackingQuery query) =>
            Value(await As(owner).Tracking.ListAsync(catalog, query)).Items.Select(item => item.Event.Id);

        (await List(new TrackingQuery { ExcludeStages = ["skipped"] })).Should().BeEquivalentTo([planning.Id, untracked.Id]);
        (await List(new TrackingQuery { Stages = [DefaultStage] })).Should().BeEquivalentTo([untracked.Id]);
        (await List(new TrackingQuery { Stage = "planning", Stages = ["skipped"] })).Should().BeEquivalentTo([planning.Id, skipped.Id]);
        (await List(new TrackingQuery { ExcludeStages = ["skipped", DefaultStage] })).Should().BeEquivalentTo([planning.Id]);
    }

    /// <summary>The summary lists an untracked event's deadline in the default stage, leaves out a lost stage's, and keeps
    /// only the kinds asked for (PG14).</summary>
    [Fact]
    public async Task GetSummaryAsync_ShouldListUntrackedDeadlines_AndOnlyTheKindsAsked()
    {
        var owner = NewMember();
        var catalog = (await CreateCatalogAsync(owner)).Slug;
        var untracked = await CreateEventAsync(owner, catalog, WithDeadline("2026-12-01", DeadlineKind.Application));
        var registration = await CreateEventAsync(owner, catalog, WithDeadline("2026-11-20", DeadlineKind.Registration));
        var skipped = await CreateEventAsync(owner, catalog, WithDeadline("2026-11-15", DeadlineKind.Application));
        Value(await As(owner).Tracking.PutAsync(catalog, registration.Id, new TrackingSpec { Stage = "planning" }));
        Value(await As(owner).Tracking.PutAsync(catalog, skipped.Id, new TrackingSpec { Stage = "skipped" }));

        var all = Value(await As(owner).Tracking.GetSummaryAsync(catalog, new DateOnly(2026, 11, 10)));
        var applications = Value(await As(owner).Tracking.GetSummaryAsync(catalog, new TrackingSummaryQuery
        {
            Today = new DateOnly(2026, 11, 10),
            DeadlineKinds = [DeadlineKind.Application],
        }));

        all.UpcomingDeadlines.Select(item => (item.EventId, item.Stage, item.Tracked)).Should().Equal(
            (registration.Id, "planning", true), (untracked.Id, DefaultStage, false));
        applications.UpcomingDeadlines.Select(item => item.EventId).Should().Equal(untracked.Id);
        all.Untracked.Should().Be(1);
    }

    private static EventFacts WithDeadline(string day, DeadlineKind kind) =>
        Facts(starts: "2027-01-10") with { Deadlines = [new EventDeadline { Kind = kind, At = new EventWhen { Value = day, Precision = DatePrecision.Day } }] };

    /// <summary>Builds facts placed in a country, or in none.</summary>
    private static EventFacts InCountry(string? country) =>
        Facts(starts: "2026-12-01") with { Place = country is null ? new EventPlace { City = "Somewhere" } : new EventPlace { City = "Capital", Country = country } };

    /// <summary>A tracking read carries each event's sources as the catalog reads them, with their name, listing state and
    /// posted time (E15); a curator's own entry names no source, state or posted time.</summary>
    [Fact]
    public async Task Reads_ShouldCarryEachSource_AsTheCatalogReadsIt()
    {
        var (owner, catalog, target) = await EventAsync();
        Value(await As(owner).Tracking.PutAsync(catalog, target.Id, new TrackingSpec()));

        var read = Value(await As(owner).Events.GetAsync(catalog, target.Id));
        var tracked = Value(await As(owner).Tracking.GetAsync(catalog, target.Id));
        var listed = Value(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { TrackedOnly = true })).Items.Single();

        read.Sources.Should().ContainSingle().Which.Should().Match<EventSourceModel>(source =>
            source.Kind == EventSourceKind.Manual && source.Name == null && source.State == null && source.PostedAtUtc == null);
        tracked.Event.Sources.Should().BeEquivalentTo(read.Sources, options => options.WithStrictOrdering());
        listed.Event.Sources.Should().BeEquivalentTo(read.Sources, options => options.WithStrictOrdering());
    }

    /// <summary>UI04: the city and kind lists list an event in any value without case, the single city and status the
    /// tracking list used to drop filter now, and <c>curated</c> lists the events no intake source reported — every event a
    /// curator creates — while a source's name lists none of them.</summary>
    [Fact]
    public async Task ListAsync_ShouldFilterByCitiesKindsStatusAndSource()
    {
        var owner = NewMember();
        var catalog = (await CreateCatalogAsync(owner)).Slug;
        var tashkent = await CreateEventAsync(owner, catalog, Placed("Tashkent", "exhibition"));
        var samarkand = await CreateEventAsync(owner, catalog, Placed("Samarkand", "fair"));
        var bukhara = await CreateEventAsync(owner, catalog, Placed("Bukhara", "exhibition") with { Status = EventStatus.Cancelled });

        async Task<IEnumerable<Guid>> List(TrackingQuery query) =>
            Value(await As(owner).Tracking.ListAsync(catalog, query)).Items.Select(item => item.Event.Id);

        (await List(new TrackingQuery { Cities = ["tashkent"] })).Should().BeEquivalentTo([tashkent.Id]);
        (await List(new TrackingQuery { Cities = ["Tashkent", "SAMARKAND"] })).Should().BeEquivalentTo([tashkent.Id, samarkand.Id]);
        (await List(new TrackingQuery { City = "bukhara", Cities = ["tashkent"] })).Should().BeEquivalentTo([tashkent.Id, bukhara.Id]);
        (await List(new TrackingQuery { Kinds = ["exhibition"] })).Should().BeEquivalentTo([tashkent.Id, bukhara.Id]);
        (await List(new TrackingQuery { Kind = "fair", Kinds = ["exhibition"] })).Should().BeEquivalentTo([tashkent.Id, samarkand.Id, bukhara.Id]);
        (await List(new TrackingQuery { Kinds = ["exhibition"], Cities = ["tashkent"] })).Should().BeEquivalentTo([tashkent.Id]);
        (await List(new TrackingQuery { Status = EventStatus.Cancelled })).Should().BeEquivalentTo([bukhara.Id]);
        (await List(new TrackingQuery { Sources = [EventsConstants.CuratedSource] })).Should().BeEquivalentTo([tashkent.Id, samarkand.Id, bukhara.Id]);
        (await List(new TrackingQuery { Sources = ["afisha"] })).Should().BeEmpty();
        (await List(new TrackingQuery { Sources = ["afisha", "CURATED"], Cities = ["samarkand"] })).Should().BeEquivalentTo([samarkand.Id]);
        (await List(new TrackingQuery { PublishedSince = DateTimeOffset.UtcNow.AddHours(-1) })).Should().BeEquivalentTo([tashkent.Id, samarkand.Id, bukhara.Id]);
        (await List(new TrackingQuery { PublishedSince = DateTimeOffset.UtcNow.AddHours(1) })).Should().BeEmpty();
    }

    /// <summary>UI04: sort <c>added</c> lists the newest published first, and a keyset cursor pages it with no repeat when an
    /// event is published between two pages; a cursor holds for the sort it came from and fails 400 under another.</summary>
    [Fact]
    public async Task ListAsync_ShouldOrderByNewestPublished_AndRefuseACursorOfAnotherSort()
    {
        var owner = NewMember();
        var catalog = (await CreateCatalogAsync(owner)).Slug;
        var first = await CreateEventAsync(owner, catalog, Facts(starts: "2027-03-01"));
        await Task.Delay(20);
        var second = await CreateEventAsync(owner, catalog, Facts(starts: "2027-01-01"));
        await Task.Delay(20);
        var third = await CreateEventAsync(owner, catalog, Facts(starts: "2027-02-01"));

        var newest = Value(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { Sort = TrackingSort.Added, Limit = 2 }));
        newest.Items.Select(item => item.Event.Id).Should().Equal(third.Id, second.Id);
        newest.NextCursor.Should().NotBeNull();

        await Task.Delay(20);
        var latecomer = await CreateEventAsync(owner, catalog, Facts(starts: "2027-04-01"));
        var rest = Value(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { Sort = TrackingSort.Added, Limit = 2, Cursor = newest.NextCursor }));
        rest.Items.Select(item => item.Event.Id).Should().Equal(first.Id);
        rest.NextCursor.Should().BeNull();

        Value(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { Sort = TrackingSort.Added })).Items.Select(item => item.Event.Id)
            .Should().Equal(latecomer.Id, third.Id, second.Id, first.Id);
        Value(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { Limit = 2 })).Items.Select(item => item.Event.Id)
            .Should().Equal(second.Id, third.Id);
        Error(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { Sort = TrackingSort.Starts, Cursor = newest.NextCursor })).Type
            .Should().Be(AppErrorType.Validation);
        Error(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { Sort = TrackingSort.Deadline, Cursor = newest.NextCursor })).Type
            .Should().Be(AppErrorType.Validation);
    }

    /// <summary>UI04: sort <c>deadline</c> lists the nearest open deadline first, and the events with none, or only a passed
    /// one, last.</summary>
    [Fact]
    public async Task ListAsync_ShouldOrderByNearestOpenDeadline_WithoutOneLast()
    {
        var owner = NewMember();
        var catalog = (await CreateCatalogAsync(owner)).Slug;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var later = await CreateEventAsync(owner, catalog, WithDeadline(today.AddDays(30)));
        var none = await CreateEventAsync(owner, catalog, Facts(starts: "2027-02-01"));
        var sooner = await CreateEventAsync(owner, catalog, WithDeadline(today.AddDays(5)));
        var passed = await CreateEventAsync(owner, catalog, WithDeadline(today.AddDays(-5)));

        var ordered = Value(await As(owner).Tracking.ListAsync(catalog, new TrackingQuery { Sort = TrackingSort.Deadline })).Items.Select(item => item.Event.Id).ToList();

        ordered.Take(2).Should().Equal(sooner.Id, later.Id);
        ordered.Skip(2).Should().BeEquivalentTo([none.Id, passed.Id]);
    }

    /// <summary>UI04: each facet counts without its own selection and the total counts the whole query — countries, cities,
    /// kinds, stages (an untracked event under the default stage), priorities, sources with <c>curated</c>, and the open
    /// deadlines inside the window.</summary>
    [Fact]
    public async Task GetFacetsAsync_ShouldCountEachFacetWithoutItsOwnSelection()
    {
        var owner = NewMember();
        var catalog = (await CreateCatalogAsync(owner)).Slug;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tashkent = await CreateEventAsync(owner, catalog, Placed("Tashkent", "exhibition") with { Deadlines = [Deadline(today.AddDays(10))] });
        await CreateEventAsync(owner, catalog, Placed("Samarkand", "fair") with { Deadlines = [Deadline(today.AddDays(90))] });
        var almaty = await CreateEventAsync(owner, catalog, Placed("Almaty", "fair", "KZ"));
        Value(await As(owner).Tracking.PutAsync(catalog, tashkent.Id, new TrackingSpec { Stage = "planning", Priority = TrackingPriority.High }));
        Value(await As(owner).Tracking.PutAsync(catalog, almaty.Id, new TrackingSpec { Stage = "going" }));

        var all = Value(await As(owner).Tracking.GetFacetsAsync(catalog, new TrackingQuery()));

        all.Total.Should().Be(3);
        Counts(all.Countries).Should().Equal(("UZ", 2), ("KZ", 1));
        Counts(all.Cities).Should().BeEquivalentTo([("tashkent", 1), ("samarkand", 1), ("almaty", 1)]);
        Counts(all.Kinds).Should().Equal(("fair", 2), ("exhibition", 1));
        Counts(all.Stages).Should().Equal(("interested", 1), ("planning", 1), ("going", 1), ("attended", 0), ("skipped", 0));
        Counts(all.Priorities).Should().Equal(("high", 1), ("normal", 1), ("low", 0));
        Counts(all.Sources).Should().Equal((EventsConstants.CuratedSource, 3));
        all.Sources.Single().Label.Should().BeNull();
        all.OpenDeadline.Count.Should().Be(1, "only Tashkent's deadline falls inside the default 30-day window");
        all.OpenDeadline.Before.Should().BeAfter(today.AddDays(28)).And.BeBefore(today.AddDays(32));

        var narrowed = Value(await As(owner).Tracking.GetFacetsAsync(catalog, new TrackingQuery { Kinds = ["fair"], Cities = ["samarkand"], DeadlineBefore = today.AddDays(120) }));

        narrowed.Total.Should().Be(1);
        Counts(narrowed.Kinds).Should().Equal(("fair", 1));
    }

    /// <summary>UI04: the facets read as the list does: another organization's private catalog is 404, a caller with no
    /// organization 403, and a query the list refuses is 400.</summary>
    [Fact]
    public async Task GetFacetsAsync_ShouldFailAsTheListDoes()
    {
        var (owner, catalog, _) = await EventAsync();

        Error(await As(NewMember()).Tracking.GetFacetsAsync(catalog, new TrackingQuery())).Type.Should().Be(AppErrorType.NotFound);
        Error(await As(EventsActor.Service()).Tracking.GetFacetsAsync(catalog, new TrackingQuery())).Type.Should().Be(AppErrorType.Forbidden);
        Error(await As(owner).Tracking.GetFacetsAsync(catalog, new TrackingQuery { Stages = ["going"], ExcludeStages = ["going"] })).Type.Should().Be(AppErrorType.Validation);
        Error(await As(owner).Tracking.GetFacetsAsync(catalog, new TrackingQuery { Countries = ["UZB"] })).Type.Should().Be(AppErrorType.Validation);
    }

    private static (string Value, int Count)[] Counts(IReadOnlyList<TrackingFacetCount> counts) => counts.Select(item => (item.Value, item.Count)).ToArray();

    /// <summary>Builds facts placed in a city and a country, of a kind, with a title of its own.</summary>
    private static EventFacts Placed(string city, string kind, string country = "UZ") =>
        Facts(starts: "2027-03-01") with { Kind = kind, Place = new EventPlace { City = city, Country = country } };

    private static EventDeadline Deadline(DateOnly day) =>
        new() { Kind = DeadlineKind.Application, At = new EventWhen { Value = day.ToString("yyyy-MM-dd"), Precision = DatePrecision.Day } };

    private static EventFacts WithDeadline(DateOnly day) => Facts(starts: "2027-03-01") with { Deadlines = [Deadline(day)] };

    /// <summary>Creates an owner, a private catalog and one published event in it.</summary>
    private async Task<(EventsActor Owner, string Catalog, EventModel Event)> EventAsync()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        return (owner, catalog.Slug, await CreateEventAsync(owner, catalog.Slug));
    }
}
