using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using WoW.Two.Sdk.Beta.Be.Events;
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
