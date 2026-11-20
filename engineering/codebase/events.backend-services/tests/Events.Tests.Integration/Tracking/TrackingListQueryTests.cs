using System.Buffers.Text;
using System.Text;
using AwesomeAssertions;
using Events.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Tracking;

/// <summary>Covers the tracking list's UI04 queries: the city, kind and source lists, the publish time, the sorts with their
/// keyset cursors, and the facets' counts each taken without its own selection.</summary>
/// <param name="fixture">The module over PostgreSQL.</param>
public sealed class TrackingListQueryTests(ModuleFixture fixture) : IClassFixture<ModuleFixture>
{
    /// <summary>The source filter matches the name each source of an event is stored under, without case; the reserved
    /// <c>curated</c> matches the events no intake source reported, and several values match any.</summary>
    [Fact]
    public async Task ListAsync_ShouldFilterBySourceName_AndCuratedForEventsNoIntakeSourceReported()
    {
        var (organization, catalog) = await StartAsync();
        var afisha = await EventAsync(catalog, Facts("Afisha show", "2026-12-01"));
        await SourceAsync(catalog, afisha, EventSourceKind.Scfd, "Afisha");
        var expo = await EventAsync(catalog, Facts("Expo show", "2026-12-02"));
        await SourceAsync(catalog, expo, EventSourceKind.Scfd, "Uzexpocentre");
        var joined = await EventAsync(catalog, Facts("Joined show", "2026-12-03"));
        await SourceAsync(catalog, joined, EventSourceKind.Scfd, "Uzexpocentre");
        await SourceAsync(catalog, joined, EventSourceKind.Scfd, "Afisha", "rule:title-date-city");
        var imported = await EventAsync(catalog, Facts("Imported show", "2026-12-04"));
        await SourceAsync(catalog, imported, EventSourceKind.Import);
        var manual = await EventAsync(catalog, Facts("Manual show", "2026-12-05"));
        await SourceAsync(catalog, manual, EventSourceKind.Manual);
        var bare = await EventAsync(catalog, Facts("Bare show", "2026-12-06"));
        var legacy = await EventAsync(catalog, Facts("Legacy show", "2026-12-07"));
        await SourceAsync(catalog, legacy, EventSourceKind.Scfd);
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sources = ["afisha"] })).Should().BeEquivalentTo([afisha.Id, joined.Id]);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sources = [" AFISHA "] })).Should().BeEquivalentTo([afisha.Id, joined.Id]);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sources = ["uzexpocentre"] })).Should().BeEquivalentTo([expo.Id, joined.Id]);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sources = ["curated"] })).Should().BeEquivalentTo([imported.Id, manual.Id, bare.Id]);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sources = ["afisha", "curated"] }))
            .Should().BeEquivalentTo([afisha.Id, joined.Id, imported.Id, manual.Id, bare.Id]);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sources = ["unknown-source"] })).Should().BeEmpty();
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery())).Should().HaveCount(7);
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sources = Enumerable.Range(0, 51).Select(index => $"s{index}").ToArray() }))
            .Error().Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>The city and kind lists match an event in any value; the single city and kind, and the status, which the
    /// tracking list used to drop, filter now.</summary>
    [Fact]
    public async Task ListAsync_ShouldFilterByCitiesKindsAndStatus_WithTheSingleValuesAddingToTheLists()
    {
        var (organization, catalog) = await StartAsync();
        var tashkent = await EventAsync(catalog, Facts("Tashkent show", "2026-12-01", "Tashkent", "UZ", "exhibition"));
        var samarkand = await EventAsync(catalog, Facts("Samarkand show", "2026-12-02", "Samarkand", "UZ", "fair"));
        var bukhara = await EventAsync(catalog, Facts("Bukhara show", "2026-12-03", "Bukhara", "UZ", "exhibition") with { Status = EventStatus.Cancelled });
        var nowhere = await EventAsync(catalog, Facts("Nowhere show", "2026-12-04", null, null, "fair"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Cities = ["tashkent"] })).Should().Equal(tashkent.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Cities = ["Tashkent", "SAMARKAND"] })).Should().Equal(tashkent.Id, samarkand.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { City = "bukhara" })).Should().Equal(bukhara.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { City = "bukhara", Cities = ["tashkent"] })).Should().Equal(tashkent.Id, bukhara.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Kinds = ["exhibition"] })).Should().Equal(tashkent.Id, bukhara.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Kind = "fair", Kinds = ["exhibition"] })).Should().HaveCount(4);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Kind = "fair" })).Should().Equal(samarkand.Id, nowhere.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Kinds = ["exhibition"], Cities = ["tashkent", "bukhara"] })).Should().Equal(tashkent.Id, bukhara.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Kinds = ["exhibition"], Cities = ["tashkent"] })).Should().Equal(tashkent.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Status = EventStatus.Cancelled })).Should().Equal(bukhara.Id);
    }

    /// <summary>The publish time lists the events published on or after it.</summary>
    [Fact]
    public async Task ListAsync_ShouldListTheEventsPublishedSinceAnInstant()
    {
        var (organization, catalog) = await StartAsync();
        var noon = fixture.Clock.GetUtcNow();
        var old = await EventAsync(catalog, Facts("Old show", "2026-12-01"), noon.AddDays(-30));
        var edge = await EventAsync(catalog, Facts("Edge show", "2026-12-02"), noon.AddDays(-1));
        var fresh = await EventAsync(catalog, Facts("Fresh show", "2026-12-03"), noon);
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { PublishedSince = noon.AddDays(-1) })).Should().Equal(edge.Id, fresh.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { PublishedSince = noon.AddHours(-12) })).Should().Equal(fresh.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { PublishedSince = noon.AddSeconds(1) })).Should().BeEmpty();
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { PublishedSince = noon.AddDays(-31) })).Should().Equal(old.Id, edge.Id, fresh.Id);
    }

    /// <summary>Sort <c>added</c> lists the newest published first and ends every tie on the id; a keyset cursor pages the
    /// whole list with no repeat and no skip, even when an event is published between two pages.</summary>
    [Fact]
    public async Task ListAsync_ShouldOrderByNewestPublished_AndPageByKeysetStableOnTies()
    {
        var (organization, catalog) = await StartAsync();
        var noon = fixture.Clock.GetUtcNow();
        var oldest = await EventAsync(catalog, Facts("A oldest", "2026-12-01"), noon.AddDays(-3));
        var tieOne = await EventAsync(catalog, Facts("B tie one", "2026-12-02"), noon.AddDays(-2));
        var tieTwo = await EventAsync(catalog, Facts("C tie two", "2026-12-03"), noon.AddDays(-2));
        var tieThree = await EventAsync(catalog, Facts("D tie three", "2026-12-04"), noon.AddDays(-2));
        var newest = await EventAsync(catalog, Facts("E newest", "2026-12-05"), noon.AddDays(-1));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        // The store orders a uuid by its bytes, which the canonical text spells in order; .NET's Guid order differs.
        var ties = new[] { tieOne.Id, tieTwo.Id, tieThree.Id }.OrderByDescending(id => id.ToString("D"), StringComparer.Ordinal).ToArray();

        var all = Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Added }));
        all.Should().Equal([newest.Id, .. ties, oldest.Id]);

        var first = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Added, Limit = 2 })).Value();
        first.Items.Select(item => item.Event.Id).Should().Equal(newest.Id, ties[0]);

        // Published between the pages, the newest event must not push a seen event onto the next page.
        var latecomer = await EventAsync(catalog, Facts("F latecomer", "2026-12-06"), noon);
        var second = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Added, Limit = 2, Cursor = first.NextCursor })).Value();
        second.Items.Select(item => item.Event.Id).Should().Equal(ties[1], ties[2]);
        var third = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Added, Limit = 2, Cursor = second.NextCursor })).Value();
        third.Items.Select(item => item.Event.Id).Should().Equal(oldest.Id);
        third.NextCursor.Should().BeNull();

        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Added, Limit = 1 })).Should().Equal(latecomer.Id);
    }

    /// <summary>Sort <c>starts</c> pages by keyset too: dated, month-precision and undated events across pages of one.</summary>
    [Fact]
    public async Task ListAsync_ShouldPageTheStartsOrderByKeyset_UndatedLast()
    {
        var (organization, catalog) = await StartAsync();
        var early = await EventAsync(catalog, Facts("Bazaar", "2026-09-01"));
        var month = await EventAsync(catalog, CatalogSeed.Facts("October fair", "2026-10", DatePrecision.Month));
        var sameDayB = await EventAsync(catalog, Facts("Textile B", "2026-12-05"));
        var sameDayA = await EventAsync(catalog, Facts("Textile A", "2026-12-05"));
        var undatedA = await EventAsync(catalog, Facts("Undated A"));
        var undatedB = await EventAsync(catalog, Facts("Undated B"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        var seen = new List<Guid>();
        string? cursor = null;
        do
        {
            var page = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Limit = 1, Cursor = cursor })).Value();
            seen.AddRange(page.Items.Select(item => item.Event.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        seen.Should().Equal(early.Id, month.Id, sameDayA.Id, sameDayB.Id, undatedA.Id, undatedB.Id);
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery())).Should().Equal(seen);
    }

    /// <summary>Sort <c>deadline</c> lists the nearest open deadline first; an event with none, or only a passed one, comes
    /// last, by title.</summary>
    [Fact]
    public async Task ListAsync_ShouldOrderByNearestOpenDeadline_WithoutOneLast()
    {
        var (organization, catalog) = await StartAsync();
        var far = await EventAsync(catalog, Facts("Far show", "2027-02-01", deadlines: [CatalogSeed.Deadline("2026-12-20")]));
        var near = await EventAsync(catalog, Facts("Near show", "2027-02-02", deadlines: [CatalogSeed.Deadline("2026-12-01"), CatalogSeed.Deadline("2027-01-15")]));
        var closer = await EventAsync(catalog, Facts("Closer show", "2027-02-03", deadlines: [CatalogSeed.Deadline("2026-11-05"), CatalogSeed.Deadline("2026-11-12")]));
        var passed = await EventAsync(catalog, Facts("Zeta passed", "2027-02-04", deadlines: [CatalogSeed.Deadline("2026-11-01")]));
        var none = await EventAsync(catalog, Facts("Alpha none", "2027-02-05"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        // The fake clock reads 2026-11-10: closer's 11-05 passed, so its open deadline is 11-12; near's is 12-01.
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Deadline }))
            .Should().Equal(closer.Id, near.Id, far.Id, none.Id, passed.Id);

        var seen = new List<Guid>();
        string? cursor = null;
        do
        {
            var page = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Deadline, Limit = 2, Cursor = cursor })).Value();
            seen.AddRange(page.Items.Select(item => item.Event.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        seen.Should().Equal(closer.Id, near.Id, far.Id, none.Id, passed.Id);
    }

    /// <summary>A cursor holds for the sort and the catalog it came from; another sort, another catalog and a cursor of the
    /// earlier offset kind are refused.</summary>
    [Fact]
    public async Task ListAsync_ShouldRefuseACursorOfAnotherSortOrCatalog()
    {
        var (organization, catalog) = await StartAsync();
        var (_, other) = await StartAsync(organization);
        foreach (var title in new[] { "One", "Two", "Three" })
        {
            await EventAsync(catalog, Facts(title, "2026-12-01"));
            await EventAsync(other, Facts(title, "2026-12-01"));
        }

        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        var byStarts = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Limit = 1 })).Value().NextCursor;
        var byAdded = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Limit = 1, Sort = TrackingSort.Added })).Value().NextCursor;
        var byDeadline = (await broker.ListAsync(catalog.Slug, new TrackingQuery { Limit = 1, Sort = TrackingSort.Deadline })).Value().NextCursor;
        byStarts.Should().NotBeNull();
        byAdded.Should().NotBeNull();

        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Added, Cursor = byStarts })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Starts, Cursor = byAdded })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Deadline, Cursor = byAdded })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Starts, Cursor = byDeadline })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.ListAsync(other.Slug, new TrackingQuery { Cursor = byStarts })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Cursor = "MXwy" })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = (TrackingSort)9 })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Added, Cursor = byAdded })).Value().Items.Should().NotBeEmpty();
    }

    /// <summary>Two events that share a start and a title (and two undated ones) keep one order under <c>starts</c>, ending on
    /// the id, so pages of one, two and three cut across the twins with neither a skip nor a repeat.</summary>
    [Fact]
    public async Task ListAsync_ShouldPageEventsThatShareStartAndTitle_UnderTheStartsOrder()
    {
        var (organization, catalog) = await StartAsync();
        var before = await EventAsync(catalog, Facts("A before", "2026-12-01"));
        var twinOne = await EventAsync(catalog, Facts("Twin show", "2026-12-05"));
        var twinTwo = await EventAsync(catalog, Facts("Twin show", "2026-12-05"));
        var after = await EventAsync(catalog, Facts("Z after", "2026-12-09"));
        var undatedOne = await EventAsync(catalog, Facts("Undated twin"));
        var undatedTwo = await EventAsync(catalog, Facts("Undated twin"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        var expected = new[] { before.Id }
            .Concat(ByStoreOrder(twinOne, twinTwo)).Append(after.Id)
            .Concat(ByStoreOrder(undatedOne, undatedTwo)).ToArray();

        foreach (var size in new[] { 1, 2, 3, 4 })
            (await PageAllAsync(broker, catalog.Slug, new TrackingQuery(), size)).Should().Equal(expected, $"pages of {size}");
        Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery())).Should().Equal(expected);
    }

    /// <summary>Two events that share their nearest open deadline and their title, and two with no open deadline and a shared
    /// title, keep one order under <c>deadline</c>, ending on the id, across every page boundary.</summary>
    [Fact]
    public async Task ListAsync_ShouldPageEventsThatShareDeadlineAndTitle_UnderTheDeadlineOrder()
    {
        var (organization, catalog) = await StartAsync();
        var soon = await EventAsync(catalog, Facts("Soon", "2027-02-01", deadlines: [CatalogSeed.Deadline("2026-11-15")]));
        var twinOne = await EventAsync(catalog, Facts("Deadline twin", "2027-02-02", deadlines: [CatalogSeed.Deadline("2026-12-01")]));
        var twinTwo = await EventAsync(catalog, Facts("Deadline twin", "2027-02-03", deadlines: [CatalogSeed.Deadline("2026-12-01")]));
        var noneOne = await EventAsync(catalog, Facts("No deadline twin", "2027-02-04"));
        var noneTwo = await EventAsync(catalog, Facts("No deadline twin", "2027-02-05", deadlines: [CatalogSeed.Deadline("2026-11-01")]));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        // The fake clock reads 2026-11-10, so noneTwo's deadline has passed and it ties with noneOne at "none".
        var expected = new[] { soon.Id }.Concat(ByStoreOrder(twinOne, twinTwo)).Concat(ByStoreOrder(noneOne, noneTwo)).ToArray();

        foreach (var size in new[] { 1, 2, 3, 4 })
            (await PageAllAsync(broker, catalog.Slug, new TrackingQuery { Sort = TrackingSort.Deadline }, size)).Should().Equal(expected, $"pages of {size}");
    }

    /// <summary>A cursor the store cannot hold is refused like one of another sort — a validation failure, never a store
    /// error: a NUL or an unpaired surrogate in its title, a value no title reaches, or a malformed day, instant or id. A tab, a
    /// line break and other control characters can sit in a real title, so they stay valid.</summary>
    [Fact]
    public async Task ListAsync_ShouldRefuseACraftedCursor_WithAValueTheStoreCannotHold()
    {
        var (organization, catalog) = await StartAsync();
        await EventAsync(catalog, Facts("One", "2026-12-01"));
        await EventAsync(catalog, Facts("Two", "2026-12-02"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        var id = "0193f3a4-0000-7000-8000-000000000001";
        var tracking = $"tracking:{catalog.Slug}";

        // Raw JSON text: the encoder cannot write an unpaired surrogate.
        string Cursor(string sort, params string[] values) =>
            Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"[\"{tracking}\",\"{sort}\"," + string.Join(',', values.Select(value => $"\"{value}\"")) + "]"));

        var refused = new[]
        {
            ("starts", new[] { "0", "2026-12-01", "bad\\u0000title", id }),
            ("starts", ["1", "", "bad\\u0000title", id]),
            ("starts", ["0", "2026-12-01", "\\ud800", id]),
            ("starts", ["0", "2026-12-01", "x\\udc00", id]),
            ("starts", ["0", "2026-12-01", new string('t', 1_001), id]),
            ("starts", ["0", "2026-13-45", "title", id]),
            ("starts", ["0", "2026-12-01", "title", "not-a-guid"]),
            ("starts", ["0", "2026-12-01", "title", id + "\\u0000"]),
            ("starts", ["2", "2026-12-01", "title", id]),
            ("deadline", ["0", "2026-12-01", "bad\\u0000title", id]),
            ("deadline", ["1", "", "\\ud800", id]),
            ("deadline", ["0", "", "title", id]),
            ("added", ["0", "-5", id]),
            ("added", ["0", "99999999999999999999999999", id]),
            ("added", ["0", "638000000000000000", "bad\\u0000id"]),
            ("added", ["1", "638000000000000000", id]),
        };
        foreach (var (sort, values) in refused)
        {
            var query = new TrackingQuery { Sort = Enum.Parse<TrackingSort>(sort, ignoreCase: true), Cursor = Cursor(sort, values) };
            (await broker.ListAsync(catalog.Slug, query)).Error().Type.Should().Be(AppErrorType.Validation, $"{sort} {string.Join('|', values).Replace('\0', '?')}");
            (await broker.GetFacetsAsync(catalog.Slug, query)).Value().Total.Should().Be(2, "the facets read no cursor");
        }

        // Titles a real event can hold, a tab and a line break among them, read as a position and never fail.
        foreach (var title in new[] { "tab\\there", "two\\nlines", "\\u0001 control", "o'brien; drop table x; --", "\\ud83c\\udfad" })
            Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Cursor = Cursor("starts", "0", "2026-12-01", title, id) })).Should().NotBeNull();
        // The first and the last instant the type holds still read.
        foreach (var ticks in new[] { "0", DateTimeOffset.MaxValue.UtcTicks.ToString() })
            Ids(await broker.ListAsync(catalog.Slug, new TrackingQuery { Sort = TrackingSort.Added, Cursor = Cursor("added", "0", ticks, id) })).Should().NotBeNull();
    }

    /// <summary>With no selection every facet counts the whole catalog: countries, cities, the catalog's kinds in its order,
    /// the workflow's stages (an untracked event in the default stage), the tracked events' priorities, the sources by name
    /// with the reserved <c>curated</c>, and the open deadlines inside the default 30-day window.</summary>
    [Fact]
    public async Task GetFacetsAsync_ShouldCountTheWholeCatalog_WithNoSelection()
    {
        var seeded = await SeedFacetCatalogAsync();
        await using var scope = fixture.Scope();

        var all = (await Broker(scope).GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery())).Value();

        all.Total.Should().Be(6);
        Counts(all.Countries).Should().Equal(("UZ", 4), ("KZ", 1));
        Counts(all.Cities).Should().Equal(("tashkent", 2), ("almaty", 1), ("bukhara", 1), ("samarkand", 1));
        Counts(all.Kinds).Should().Equal(("exhibition", 2), ("fair", 3), ("conference", 0), ("other", 1));
        Counts(all.Stages).Should().Equal(("interested", 2), ("planning", 2), ("going", 1), ("attended", 0), ("skipped", 1));
        Counts(all.Priorities).Should().Equal(("high", 1), ("normal", 2), ("low", 1));
        Counts(all.Sources).Should().Equal(("afisha", 3), ("curated", 2), ("uzexpocentre", 2));
        all.Sources.Where(source => source.Value != "curated").Select(source => source.Label).Should().Equal("Afisha", "Uzexpocentre");
        all.OpenDeadline.Should().Be(new TrackingDeadlineFacet(new DateOnly(2026, 12, 10), 1));
    }

    /// <summary>Each facet counts the events its own chips would list under every other selection: a selected city leaves
    /// the city counts alone and narrows the rest, a selected source the source counts, and the total counts the query.</summary>
    [Fact]
    public async Task GetFacetsAsync_ShouldCountEachFacetWithoutItsOwnSelection()
    {
        var seeded = await SeedFacetCatalogAsync();
        await using var scope = fixture.Scope();

        var facets = (await Broker(scope).GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { Cities = ["tashkent"], Sources = ["afisha"] })).Value();

        facets.Total.Should().Be(1, "Tashkent and Afisha: the week alone");
        Counts(facets.Countries).Should().Equal(("UZ", 1));
        // The city selection is left out of the city counts, and Afisha holds on.
        Counts(facets.Cities).Should().Equal(("samarkand", 1), ("tashkent", 1));
        Counts(facets.Kinds).Should().Equal(("exhibition", 1), ("fair", 0), ("conference", 0));
        Counts(facets.Stages).Should().Equal(("interested", 0), ("planning", 1), ("going", 0), ("attended", 0), ("skipped", 0));
        Counts(facets.Priorities).Should().Equal(("high", 1), ("normal", 0), ("low", 0));
        // The source selection is left out of the source counts, and Tashkent holds on.
        Counts(facets.Sources).Should().Equal(("afisha", 1), ("curated", 1));
        facets.OpenDeadline.Count.Should().Be(1);
    }

    /// <summary>A selected country (the list, the single value, the left-out list and the unknown-country flag) leaves the country
    /// counts alone, while every other facet narrows to it.</summary>
    [Fact]
    public async Task GetFacetsAsync_ShouldLeaveOutTheCountrySelections_OfTheCountryCounts()
    {
        var seeded = await SeedFacetCatalogAsync();
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        var kazakh = (await broker.GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { Countries = ["KZ"], Kinds = ["fair"] })).Value();
        kazakh.Total.Should().Be(1, "Almaty: in Kazakhstan and a fair");
        // The fairs of every country (the kinds selection holds, the country selection does not).
        Counts(kazakh.Countries).Should().Equal(("KZ", 1), ("UZ", 1));
        Counts(kazakh.Cities).Should().Equal(("almaty", 1));
        Counts(kazakh.Kinds).Should().Equal(("exhibition", 0), ("fair", 1), ("conference", 0));
        Counts(kazakh.Stages).Should().Equal(("interested", 0), ("planning", 1), ("going", 0), ("attended", 0), ("skipped", 0));
        Counts(kazakh.Priorities).Should().Equal(("high", 0), ("normal", 0), ("low", 1));
        Counts(kazakh.Sources).Should().Equal(("curated", 1));
        kazakh.OpenDeadline.Count.Should().Be(0);

        var single = (await broker.GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { Country = "kz" })).Value();
        single.Total.Should().Be(1);
        Counts(single.Countries).Should().Equal(("UZ", 4), ("KZ", 1));
        Counts(single.Cities).Should().Equal(("almaty", 1));

        var abroad = (await broker.GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { ExcludeCountries = ["UZ"], IncludeUnknownCountry = true })).Value();
        abroad.Total.Should().Be(2, "Almaty, and the fair with no country");
        Counts(abroad.Countries).Should().Equal(("UZ", 4), ("KZ", 1));
        Counts(abroad.Kinds).Should().Equal(("exhibition", 0), ("fair", 2), ("conference", 0));
        Counts(abroad.Cities).Should().Equal(("almaty", 1));
        Counts(abroad.Stages).Should().Equal(("interested", 0), ("planning", 1), ("going", 1), ("attended", 0), ("skipped", 0));
    }

    /// <summary>A selected stage leaves the stage counts alone; a priority implies tracked events for every other facet; the
    /// deadline selection only sets the open-deadline window's end, which that facet counts inside.</summary>
    [Fact]
    public async Task GetFacetsAsync_ShouldLeaveOutStagePriorityAndDeadlineSelections_OfTheirOwnFacets()
    {
        var seeded = await SeedFacetCatalogAsync();
        await using var scope = fixture.Scope();
        var broker = Broker(scope);

        var byStage = (await broker.GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { Stages = ["planning"], Kinds = ["fair"] })).Value();
        byStage.Total.Should().Be(1, "Almaty: planning and a fair");
        Counts(byStage.Stages).Should().Equal(("interested", 1), ("planning", 1), ("going", 1), ("attended", 0), ("skipped", 0));
        Counts(byStage.Kinds).Should().Equal(("exhibition", 1), ("fair", 1), ("conference", 0));

        var byPriority = (await broker.GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { Priority = TrackingPriority.High })).Value();
        byPriority.Total.Should().Be(1);
        Counts(byPriority.Priorities).Should().Equal(("high", 1), ("normal", 2), ("low", 1));
        Counts(byPriority.Stages).Should().Equal(("interested", 0), ("planning", 1), ("going", 0), ("attended", 0), ("skipped", 0));

        var byDeadline = (await broker.GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { DeadlineBefore = new DateOnly(2027, 2, 1) })).Value();
        byDeadline.Total.Should().Be(2, "the week and the craft fair; the maker day's deadline has passed");
        byDeadline.OpenDeadline.Should().Be(new TrackingDeadlineFacet(new DateOnly(2027, 2, 1), 2));
        Counts(byDeadline.Stages).Should().Equal(("interested", 1), ("planning", 1), ("going", 0), ("attended", 0), ("skipped", 0));

        var planningWindow = (await broker.GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { Stages = ["planning"], DeadlineBefore = new DateOnly(2026, 11, 15) })).Value();
        planningWindow.Total.Should().Be(0);
        planningWindow.OpenDeadline.Should().Be(new TrackingDeadlineFacet(new DateOnly(2026, 11, 15), 0));

        var text = (await broker.GetFacetsAsync(seeded.Catalog.Slug, new TrackingQuery { Text = "textile" })).Value();
        text.Total.Should().Be(1);
        Counts(text.Cities).Should().Equal(("bukhara", 1));
    }

    /// <summary>The facets read the caller organization's tracking only, and refuse what the list refuses.</summary>
    [Fact]
    public async Task GetFacetsAsync_ShouldCountAnotherOrganizationsEventsAsUntracked_AndFailAsTheListDoes()
    {
        var owner = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, owner, CatalogVisibility.Public);
        var item = await EventAsync(catalog, Facts("Shared show", "2026-12-01", "Tashkent", "UZ", "fair"));
        var privateCatalog = await CatalogSeed.CatalogAsync(fixture, owner);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        fixture.Principal.ActAsMember(owner);
        (await broker.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "going", Priority = TrackingPriority.High })).Value();

        var own = (await broker.GetFacetsAsync(catalog.Slug, new TrackingQuery())).Value();
        Counts(own.Stages).Should().Contain(("going", 1));
        Counts(own.Priorities).Should().Contain(("high", 1));

        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var other = (await broker.GetFacetsAsync(catalog.Slug, new TrackingQuery())).Value();
        other.Total.Should().Be(1);
        Counts(other.Stages).Should().Contain(("interested", 1)).And.Contain(("going", 0));
        Counts(other.Priorities).Should().Equal(("high", 0), ("normal", 0), ("low", 0));
        (await broker.GetFacetsAsync(privateCatalog.Slug, new TrackingQuery())).Error().Type.Should().Be(AppErrorType.NotFound);

        fixture.Principal.ActAsMember(owner);
        (await broker.GetFacetsAsync(catalog.Slug, new TrackingQuery { Stages = ["going"], ExcludeStages = ["going"] })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.GetFacetsAsync(catalog.Slug, new TrackingQuery { Countries = ["UZB"] })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.GetFacetsAsync(catalog.Slug, new TrackingQuery { Cities = new string[51].Select((_, index) => $"c{index}").ToArray() })).Error().Type.Should().Be(AppErrorType.Validation);
        fixture.Principal.ActAsAnonymous();
        (await broker.GetFacetsAsync(catalog.Slug, new TrackingQuery())).Error().Type.Should().Be(AppErrorType.Forbidden);
    }

    /// <summary>Seeds six events across four cities, three kinds, three source shapes and four stages; the organization
    /// that owns the catalog tracks four of them.</summary>
    private async Task<FacetCatalog> SeedFacetCatalogAsync()
    {
        var (organization, catalog) = await StartAsync(kinds: ["exhibition", "fair", "conference"]);
        var week = await EventAsync(catalog, Facts("Tashkent Fashion Week", "2027-03-01", "Tashkent", "UZ", "exhibition", CatalogSeed.Deadline("2026-11-20")));
        await SourceAsync(catalog, week, EventSourceKind.Scfd, "Afisha");
        var craft = await EventAsync(catalog, Facts("Samarkand Craft Fair", "2027-03-02", "Samarkand", "UZ", "fair", CatalogSeed.Deadline("2027-01-10")));
        await SourceAsync(catalog, craft, EventSourceKind.Scfd, "Afisha");
        var expo = await EventAsync(catalog, Facts("Bukhara Textile Expo", "2027-03-03", "Bukhara", "UZ", "exhibition"));
        await SourceAsync(catalog, expo, EventSourceKind.Scfd, "Uzexpocentre");
        var almaty = await EventAsync(catalog, Facts("Almaty Show", "2027-03-04", "Almaty", "KZ", "fair"));
        await SourceAsync(catalog, almaty, EventSourceKind.Import);
        await EventAsync(catalog, Facts("Tashkent Maker Day", "2027-03-05", "Tashkent", "UZ", "other", CatalogSeed.Deadline("2026-11-01")));
        var unplaced = await EventAsync(catalog, Facts("Unplaced fair", "2027-03-06", null, null, "fair"));
        await SourceAsync(catalog, unplaced, EventSourceKind.Scfd, "Uzexpocentre");
        await SourceAsync(catalog, unplaced, EventSourceKind.Scfd, "Afisha", "rule:title-date-city");

        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        (await broker.PutAsync(catalog.Slug, week.Id, new TrackingSpec { Stage = "planning", Priority = TrackingPriority.High })).Value();
        (await broker.PutAsync(catalog.Slug, expo.Id, new TrackingSpec { Stage = "skipped" })).Value();
        (await broker.PutAsync(catalog.Slug, almaty.Id, new TrackingSpec { Stage = "planning", Priority = TrackingPriority.Low })).Value();
        (await broker.PutAsync(catalog.Slug, unplaced.Id, new TrackingSpec { Stage = "going" })).Value();
        return new FacetCatalog(organization, catalog);
    }

    private sealed record FacetCatalog(string Organization, CatalogEntity Catalog);

    /// <summary>Orders events as the store orders their ids: by the bytes, which the canonical text spells in order.</summary>
    private static IEnumerable<Guid> ByStoreOrder(params CatalogEventEntity[] events) =>
        events.Select(item => item.Id).OrderBy(id => id.ToString("D"), StringComparer.Ordinal);

    /// <summary>Reads every page of a list by its cursors, pages of the size given.</summary>
    private static async Task<List<Guid>> PageAllAsync(IEventTrackingBroker broker, string slug, TrackingQuery query, int limit)
    {
        var seen = new List<Guid>();
        string? cursor = null;
        do
        {
            var page = (await broker.ListAsync(slug, query with { Limit = limit, Cursor = cursor })).Value();
            seen.AddRange(page.Items.Select(item => item.Event.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return seen;
    }

    private static (string Value, int Count)[] Counts(IReadOnlyList<TrackingFacetCount> counts) =>
        counts.Select(item => (item.Value, item.Count)).ToArray();

    private async Task<(string Organization, CatalogEntity Catalog)> StartAsync(string? organization = null, string[]? kinds = null)
    {
        organization ??= ModuleFixture.NewOrganization();
        return (organization, await CatalogSeed.CatalogAsync(fixture, organization, CatalogVisibility.Private, "Fashion events", kinds ?? []));
    }

    private Task<CatalogEventEntity> EventAsync(CatalogEntity catalog, EventFacts facts, DateTimeOffset? publishedAt = null) =>
        CatalogSeed.EventAsync(fixture, catalog, facts, EventReviewState.Published, publishedAt);

    private Task<EventSourceEntity> SourceAsync(CatalogEntity catalog, CatalogEventEntity item, EventSourceKind kind, string? name = null, string? matchedBy = null) =>
        CatalogSeed.SourceAsync(fixture, catalog, item, kind, name, matchedBy);

    private static EventFacts Facts(string title, string? starts = null, string? city = null, string? country = null, string kind = "other", params EventDeadline[] deadlines) =>
        CatalogSeed.Facts(title, starts, deadlines: deadlines) with
        {
            Kind = kind,
            Place = city is null && country is null ? null : new EventPlace { City = city, Country = country },
        };

    private static IReadOnlyList<Guid> Ids(Result<TrackingPage> result) => result.Value().Items.Select(item => item.Event.Id).ToList();

    private static IEventTrackingBroker Broker(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IEventTrackingBroker>();
}
