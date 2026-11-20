using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Identity.Testing;
using static Events.Tests.E2E.EventsApiRequests;

namespace Events.Tests.E2E;

/// <summary>The tracking list's UI04 queries and its facets over the wire: the new filters and sorts, the keyset cursor bound
/// to its sort, the facets route's camelCase body, and the list's access rules on the new route.</summary>
[Collection(EventsApiCollection.Name)]
public sealed class TrackingListApiTests(EventsApiFixture fixture)
{
    private static readonly string[] AllScopes =
    [
        EventsScopeConstants.Read, EventsScopeConstants.Track, EventsScopeConstants.Review, EventsScopeConstants.Import,
        EventsScopeConstants.Admin,
    ];

    private static string Tracking(string slug, string query = "") => $"{Catalogs}/{slug}/{EventsRouteConstants.Tracking}{query}";

    private static string Facets(string slug, string query = "") => $"{Catalogs}/{slug}/{EventsRouteConstants.Tracking}/facets{query}";

    [Fact]
    public async Task List_ShouldFilterByCity_SortByNewestPublished_AndRefuseACursorOfAnotherSort()
    {
        var (slug, client) = await SeedAsync();

        using var tashkent = await client.GetAsync(Tracking(slug, "?cities=tashkent&cities=samarkand&kinds=fair"));
        tashkent.StatusCode.Should().Be(HttpStatusCode.OK, await tashkent.Content.ReadAsStringAsync());
        Titles(await DataAsync(tashkent)).Should().BeEquivalentTo(["Tashkent fair", "Samarkand fair"]);

        using var curated = await client.GetAsync(Tracking(slug, "?sources=curated&sources=afisha&city=bukhara&status=scheduled"));
        Titles(await DataAsync(curated)).Should().Equal("Bukhara expo");

        using var added = await client.GetAsync(Tracking(slug, "?sort=added&limit=2"));
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await DataAsync(added);
        Titles(page).Should().Equal("Bukhara expo", "Samarkand fair");
        var cursor = page.GetProperty("nextCursor").GetString()!;

        using var rest = await client.GetAsync(Tracking(slug, $"?sort=added&limit=2&cursor={Uri.EscapeDataString(cursor)}"));
        Titles(await DataAsync(rest)).Should().Equal("Tashkent fair");

        using var wrongSort = await client.GetAsync(Tracking(slug, $"?sort=starts&cursor={Uri.EscapeDataString(cursor)}"));
        using var noSort = await client.GetAsync(Tracking(slug, $"?cursor={Uri.EscapeDataString(cursor)}"));
        wrongSort.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        noSort.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var deadline = await client.GetAsync(Tracking(slug, "?sort=deadline"));
        deadline.StatusCode.Should().Be(HttpStatusCode.OK, await deadline.Content.ReadAsStringAsync());
        Titles(await DataAsync(deadline)).Should().Equal("Samarkand fair", "Tashkent fair", "Bukhara expo");
    }

    [Fact]
    public async Task List_ShouldAnswer400ForACraftedCursor_NeverAStoreError()
    {
        var (slug, client) = await SeedAsync();
        var id = Guid.NewGuid().ToString("D");
        string Crafted(string sort, params string[] values) => Uri.EscapeDataString(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $"[\"tracking:{slug}\",\"{sort}\"," + string.Join(',', values.Select(value => $"\"{value}\"")) + "]")));

        foreach (var (sort, cursor) in new[]
        {
            ("starts", Crafted("starts", "0", "2027-03-01", "bad\\u0000title", id)),
            ("starts", Crafted("starts", "0", "2027-03-01", "\\ud800", id)),
            ("deadline", Crafted("deadline", "0", "2027-03-01", "bad\\u0000title", id)),
            ("added", Crafted("added", "0", "not-ticks", id)),
            ("starts", Uri.EscapeDataString(Base64Url.EncodeToString("not json"u8))),
            ("starts", "%00"),
        })
        {
            using var response = await client.GetAsync(Tracking(slug, $"?sort={sort}&cursor={cursor}"));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{sort} {cursor}");
        }

        using var control = await client.GetAsync(Tracking(slug, $"?cursor={Crafted("starts", "0", "2027-03-01", "tab\\there", id)}"));
        control.StatusCode.Should().Be(HttpStatusCode.OK, "a tab can sit in a real title");
    }

    [Fact]
    public async Task Facets_ShouldAnswerTheCountsInCamelCase_EachWithoutItsOwnSelection()
    {
        var (slug, client) = await SeedAsync();

        using var response = await client.GetAsync(Facets(slug, "?kinds=fair&sort=added&limit=1&cursor=ignored"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var data = await DataAsync(response);
        data.GetProperty("total").GetInt32().Should().Be(2);
        Counts(data, "kinds").Should().Equal(("fair", 2), ("exhibition", 1));
        Counts(data, "cities").Should().BeEquivalentTo([("tashkent", 1), ("samarkand", 1)]);
        Counts(data, "countries").Should().Equal(("UZ", 2));
        Counts(data, "stages").Should().Contain(("interested", 2));
        Counts(data, "priorities").Should().Equal(("high", 0), ("normal", 0), ("low", 0));
        Counts(data, "sources").Should().Equal(("curated", 2));
        data.GetProperty("openDeadline").GetProperty("count").GetInt32().Should().Be(1);
        DateOnly.Parse(data.GetProperty("openDeadline").GetProperty("before").GetString()!).Should().Be(Today().AddDays(EventsConstants.DeadlineWindowDays));
    }

    [Fact]
    public async Task Facets_ShouldAnswer400ForABadQuery_403WithoutTheReadPermission_404ForAnotherOrganization_AndNeedsAToken()
    {
        var (slug, client) = await SeedAsync();
        var reader = Person(EventsPermissionConstants.Track);
        using var withoutPermission = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(reader, "desk", EventsApiFixture.Audience, EventsScopeConstants.Read));
        var outsider = Person(EventsPermissionConstants.Read);
        using var other = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(outsider, "desk", EventsApiFixture.Audience, AllScopes));
        using var anonymous = fixture.Host.CreateClient();

        foreach (var bad in new[] { "?sort=sideways", "?publishedSince=yesterday", "?status=gone", "?countries=UZB", "?stages=going&excludeStages=going" })
        {
            using var failed = await client.GetAsync(Facets(slug, bad));
            failed.StatusCode.Should().Be(HttpStatusCode.BadRequest, bad);
        }

        using var forbidden = await withoutPermission.GetAsync(Facets(slug));
        using var notFound = await other.GetAsync(Facets(slug));
        using var unauthorized = await anonymous.GetAsync(Facets(slug));

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var yesterday = fixture.Host.Clock.GetUtcNow().AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var tomorrow = fixture.Host.Clock.GetUtcNow().AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        using var since = await client.GetAsync(Facets(slug, $"?publishedSince={Uri.EscapeDataString(yesterday)}"));
        using var future = await client.GetAsync(Facets(slug, $"?publishedSince={Uri.EscapeDataString(tomorrow)}"));
        since.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DataAsync(since)).GetProperty("total").GetInt32().Should().Be(3);
        (await DataAsync(future)).GetProperty("total").GetInt32().Should().Be(0);
    }

    /// <summary>Gets the day the host's clock reads: the E2E host runs on a fake clock, so dates are set against it.</summary>
    private DateOnly Today() => DateOnly.FromDateTime(fixture.Host.Clock.GetUtcNow().UtcDateTime);

    private static IEnumerable<string> Titles(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("event").GetProperty("facts").GetProperty("title").GetProperty("und").GetString()!);

    private static (string Value, int Count)[] Counts(JsonElement data, string facet) =>
        data.GetProperty(facet).EnumerateArray().Select(item => (item.GetProperty("value").GetString()!, item.GetProperty("count").GetInt32())).ToArray();

    /// <summary>Creates a private catalog and three events by hand, one after the other, as a curator of a fresh organization.</summary>
    private async Task<(string Slug, HttpClient Client)> SeedAsync()
    {
        var curator = Person(EventsPermissionConstants.Admin, EventsPermissionConstants.Read, EventsPermissionConstants.Review, EventsPermissionConstants.Track);
        var slug = $"ui04-{Guid.NewGuid():N}";
        var client = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(curator, "desk", EventsApiFixture.Audience, AllScopes));
        using (var put = await client.PutAsync($"{Catalogs}/{slug}", Json("""{"name":"Tracking catalog","visibility":"private"}""")))
            put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        // The host's clock stands still, so each event is published a second after the one before.
        var soon = Today().AddDays(7).ToString("yyyy-MM-dd");
        var later = Today().AddDays(90).ToString("yyyy-MM-dd");
        await CreateEventAsync(client, slug, "Tashkent fair", "fair", "Tashkent", "2027-03-01", later);
        fixture.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        await CreateEventAsync(client, slug, "Samarkand fair", "fair", "Samarkand", "2027-03-02", soon);
        fixture.Host.Clock.Advance(TimeSpan.FromSeconds(1));
        await CreateEventAsync(client, slug, "Bukhara expo", "exhibition", "Bukhara", "2027-03-03", null);
        return (slug, client);
    }

    private static async Task CreateEventAsync(HttpClient client, string slug, string title, string kind, string city, string starts, string? deadline)
    {
        var deadlines = deadline is null ? "[]" : $$$"""[{"kind":"application","at":{"value":"{{{deadline}}}","precision":"day"}}]""";
        using var response = await client.PostAsync($"{Catalogs}/{slug}/{EventsRouteConstants.Events}", Json($$$"""
            {"facts":{"title":{"und":"{{{title}}}"},"kind":"{{{kind}}}","place":{"city":"{{{city}}}","country":"UZ"},
             "starts":{"value":"{{{starts}}}","precision":"day"},"deadlines":{{{deadlines}}}}}
            """));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }
}
