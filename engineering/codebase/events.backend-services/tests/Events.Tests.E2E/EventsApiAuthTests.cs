using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Signals;
using WoW.Two.Sdk.Beta.Be.Messaging.Buses;
using static Events.Tests.E2E.EventsApiRequests;

namespace Events.Tests.E2E;

/// <summary>Who may call the HTTP mirror: a person needs the app's scope and their own permission, a service its scope
/// alone; a token for another audience or a revoked session's token counts for nothing (product § Access).</summary>
[Collection(EventsApiCollection.Name)]
public sealed class EventsApiAuthTests(EventsApiFixture fixture)
{
    private static readonly string[] AllScopes =
    [
        EventsScopeConstants.Read, EventsScopeConstants.Track, EventsScopeConstants.Review, EventsScopeConstants.Import,
        EventsScopeConstants.Admin,
    ];

    [Fact]
    public async Task Read_ShouldAnswer401_WithoutAToken()
    {
        using var client = fixture.Host.CreateClient();

        using var response = await client.GetAsync(Catalogs);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Person_ShouldAnswer403_WhenTheAppHasTheScopeButThePersonLacksThePermission()
    {
        var person = Person(EventsPermissionConstants.Track);
        using var client = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(person, "desk", EventsApiFixture.Audience, EventsScopeConstants.Read));

        using var response = await client.GetAsync(Catalogs);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Person_ShouldAnswer403_WhenThePersonHasThePermissionButTheAppLacksTheScope()
    {
        var person = Person(EventsPermissionConstants.Read);
        using var client = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(person, "desk", EventsApiFixture.Audience, EventsScopeConstants.Track));

        using var response = await client.GetAsync(Catalogs);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Person_ShouldAnswer401_ForATokenOfAnotherAudience()
    {
        var person = Person(EventsPermissionConstants.Read);
        using var client = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(person, "desk", "https://files.example.test/", EventsScopeConstants.Read));

        using var response = await client.GetAsync(Catalogs);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Person_ShouldAnswer403_OnAWorkflowWrite_WithoutTheAdminPermission()
    {
        var person = Person(EventsPermissionConstants.Read, EventsPermissionConstants.Track);
        using var client = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(person, "desk", EventsApiFixture.Audience, EventsScopeConstants.Track));

        using var response = await client.PutAsync("/" + EventsRouteConstants.Workflow, Json("""{"stages":[{"key":"open"}],"defaultStage":"open"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Service_ShouldReadAPublicCatalog_WithTheScopeAlone()
    {
        var owner = Person(EventsPermissionConstants.Admin, EventsPermissionConstants.Read);
        var slug = $"public-{Guid.NewGuid():N}";
        using (var admin = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(owner, "desk", EventsApiFixture.Audience, AllScopes)))
        using (var put = await admin.PutAsync($"{Catalogs}/{slug}", Json("""{"name":"Open catalog","visibility":"public"}""")))
            put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        using var service = Bearer(fixture.Host, fixture.Issuer.IssueServiceToken("harvest", EventsApiFixture.Audience, EventsScopeConstants.Read));
        using var response = await service.GetAsync($"{Catalogs}/{slug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var catalog = await DataAsync(response);
        catalog.GetProperty("slug").GetString().Should().Be(slug);
        catalog.GetProperty("visibility").GetString().Should().Be("public");
    }

    [Fact]
    public async Task Service_ShouldAnswer403_WithoutTheScope()
    {
        using var service = Bearer(fixture.Host, fixture.Issuer.IssueServiceToken("harvest", EventsApiFixture.Audience, EventsScopeConstants.Track));

        using var response = await service.GetAsync(Catalogs);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Person_ShouldAnswer401_OnceIdentitySignalsTheirSessionEnded()
    {
        var person = Person(EventsPermissionConstants.Read);
        using var client = Bearer(fixture.Host, fixture.Issuer.IssueExchangedToken(person, "desk", EventsApiFixture.Audience, EventsScopeConstants.Read));
        using (var before = await client.GetAsync(Catalogs))
            before.StatusCode.Should().Be(HttpStatusCode.OK);

        // A verified signal lands on the local bus; publishing there runs the same deny-list handler.
        await fixture.Host.Services.GetRequiredService<IEventBus>().PublishAsync(new SessionEndedEvent
        {
            Issuer = fixture.Issuer.Issuer,
            SessionId = person.SessionId!.Value.ToString("D"),
            Subject = person.Id.ToString("D"),
            At = DateTimeOffset.UtcNow,
            TokensExpireBy = DateTimeOffset.UtcNow.AddMinutes(10),
        });

        var status = HttpStatusCode.OK;
        for (var attempt = 0; attempt < 20 && status != HttpStatusCode.Unauthorized; attempt++)
        {
            using var after = await client.GetAsync(Catalogs);
            status = after.StatusCode;
            if (status != HttpStatusCode.Unauthorized)
                await Task.Delay(100);
        }

        status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signal_ShouldAnswer401_WhenItCarriesNoSignature()
    {
        using var client = fixture.Host.CreateClient();

        using var response = await client.PostAsync("/" + EventsRouteConstants.Signals, Json("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Browser_ShouldAnswer401_WhenTheConsoleIsNotConfigured()
    {
        using var client = fixture.Host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Catalogs);
        request.Headers.Add("Cookie", ".events.session=stale");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
