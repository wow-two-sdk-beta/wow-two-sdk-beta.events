using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Identity.Testing;
using static Events.Tests.E2E.EventsApiRequests;

namespace Events.Tests.E2E;

/// <summary>A catalog's intake freshness over the wire (E23): any member of the owner organization with <c>events:read</c>
/// reads it, not only an admin; another organization's call answers 404 as the catalog itself does, and the answer carries
/// times and a count only. The test host configures no intake source, so the figures read as never received.</summary>
[Collection(EventsApiCollection.Name)]
public sealed class CatalogFreshnessTests(EventsApiFixture fixture)
{
    private static readonly string[] AllScopes =
    [
        EventsScopeConstants.Read, EventsScopeConstants.Track, EventsScopeConstants.Review, EventsScopeConstants.Import,
        EventsScopeConstants.Admin,
    ];

    private static string Freshness(string slug) => $"{Catalogs}/{slug}/{EventsRouteConstants.Freshness}";

    [Fact]
    public async Task Freshness_ShouldAnswer200_ToAMemberWhoOnlyReads_AndCarryTimesAndACountOnly()
    {
        var (slug, admin) = await CreateCatalogAsync();
        var member = admin with { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), Permissions = [EventsPermissionConstants.Read] };
        using var client = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(member, "desk", EventsApiFixture.Audience, EventsScopeConstants.Read));

        using var response = await client.GetAsync(Freshness(slug));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var data = await DataAsync(response);
        data.EnumerateObject().Select(property => property.Name).Should().BeSubsetOf(["lastReceivedAtUtc", "newestObservedAtUtc", "failingSources"]);
        data.GetProperty("failingSources").GetInt32().Should().Be(0);
        foreach (var time in new[] { "lastReceivedAtUtc", "newestObservedAtUtc" })
            (!data.TryGetProperty(time, out var value) || value.ValueKind == JsonValueKind.Null).Should().BeTrue($"{time} reads null with no source");

        using var status = await client.GetAsync("/" + EventsRouteConstants.Intake);
        status.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the operator's status still needs events.admin");
    }

    [Fact]
    public async Task Freshness_ShouldAnswer404_ToAnotherOrganization_AndToAServiceWithNoOrganization()
    {
        var (slug, _) = await CreateCatalogAsync();
        var outsider = Person(EventsPermissionConstants.Read, EventsPermissionConstants.Admin);
        using var other = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(outsider, "desk", EventsApiFixture.Audience, AllScopes));
        using var service = Bearer(fixture.Host, fixture.Issuer.IssueServiceToken("harvest", EventsApiFixture.Audience, EventsScopeConstants.Read));

        using var byOther = await other.GetAsync(Freshness(slug));
        using var byService = await service.GetAsync(Freshness(slug));
        using var unknown = await other.GetAsync(Freshness($"missing-{Guid.NewGuid():N}"));

        byOther.StatusCode.Should().Be(HttpStatusCode.NotFound);
        byService.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Freshness_ShouldAnswer401_WithoutAToken_And403_WithoutThePermissionOrTheScope()
    {
        var (slug, admin) = await CreateCatalogAsync();
        var tracker = admin with { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), Permissions = [EventsPermissionConstants.Track] };
        using var anonymous = fixture.Host.CreateClient();
        using var withoutPermission = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(tracker, "desk", EventsApiFixture.Audience, EventsScopeConstants.Read));
        using var withoutScope = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(admin, "desk", EventsApiFixture.Audience, EventsScopeConstants.Track));

        using var noToken = await anonymous.GetAsync(Freshness(slug));
        using var noPermission = await withoutPermission.GetAsync(Freshness(slug));
        using var noScope = await withoutScope.GetAsync(Freshness(slug));

        noToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        noPermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        noScope.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Creates a private catalog as an admin of a fresh organization.</summary>
    private async Task<(string Slug, FakePerson Admin)> CreateCatalogAsync()
    {
        var admin = Person(EventsPermissionConstants.Admin, EventsPermissionConstants.Read);
        var slug = $"fresh-{Guid.NewGuid():N}";
        using var client = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(admin, "desk", EventsApiFixture.Audience, AllScopes));
        using var put = await client.PutAsync($"{Catalogs}/{slug}", Json("""{"name":"Freshness catalog","visibility":"private"}"""));
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return (slug, admin);
    }
}
