using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using static Events.Tests.E2E.EventsApiRequests;

namespace Events.Tests.E2E;

/// <summary>The review console's sign-in, composed when <c>Events:Console</c> names an authority and a client id: Identity's
/// BFF session routes under <c>/api/identity</c>, a browser without a session answers 401 on the API, and tokens keep
/// working beside it.</summary>
[Collection(EventsApiCollection.Name)]
public sealed class EventsConsoleTests(EventsApiFixture fixture)
{
    [Fact]
    public async Task Session_ShouldAnswerSignedOut_WithoutASession()
    {
        using var client = Browser();

        using var response = await client.GetAsync("/api/identity/session");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DataAsync(response)).GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Api_ShouldAnswerABrowser401_WithoutASession()
    {
        using var client = Browser();

        using var response = await client.GetAsync(Catalogs);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Api_ShouldStillAcceptATokenBesideTheConsole()
    {
        var person = Person(EventsPermissionConstants.Read);
        using var client = Bearer(fixture.ConsoleHost, fixture.Issuer.IssueExchangedToken(person, "desk", EventsApiFixture.Audience, EventsScopeConstants.Read));

        using var response = await client.GetAsync(Catalogs);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Identity grants <c>events.*</c> to the console's client only through token exchange, and refuses a
    /// sign-in that asks for them (ocharo-dev, 2026-11-10), so the sign-in asks for the BFF's defaults only.</summary>
    [Fact]
    public void SignIn_ShouldRequestNoEventsScope()
    {
        var bff = fixture.ConsoleHost.Services.GetRequiredService<IOptions<IdentityBffOptions>>().Value;

        bff.Scopes.Should().NotContain(scope => scope.StartsWith("events.", StringComparison.Ordinal));
    }

    /// <summary>A console session is Events' own app: it carries the five API scopes, and the person's permissions from
    /// the sign-in still gate each action.</summary>
    [Fact]
    public async Task Session_ShouldCarryTheConsolesApiScopes()
    {
        using var scope = fixture.ConsoleHost.Services.CreateScope();
        var transformations = scope.ServiceProvider.GetServices<IClaimsTransformation>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "person-1")], BffAuthenticationSchemeConstants.Cookie));

        foreach (var transformation in transformations)
            principal = await transformation.TransformAsync(principal);

        principal.FindFirst("scope")!.Value.Split(' ').Should().BeEquivalentTo(
            EventsScopeConstants.Read, EventsScopeConstants.Review, EventsScopeConstants.Track, EventsScopeConstants.Import, EventsScopeConstants.Admin);
    }

    [Fact]
    public async Task ConsolePages_ShouldServe_WithoutASession()
    {
        using var client = Browser();

        using var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }

    private HttpClient Browser() => fixture.ConsoleHost.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });
}
