using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Client;
using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2;
using WoW.Two.Sdk.Beta.Be.Identity.Testing;

namespace Events.Tests.Unit.Client;

/// <summary>The Events client's own Identity wiring: one <c>AddEventsClient</c> call with <c>Identity</c> set exchanges
/// the person's token for one with Events' audience, or sends the service's own, so a consumer writes no grant options.</summary>
public sealed class EventsClientIdentityTests : IDisposable
{
    private const string ClientId = "desk";
    private const string EventsResource = "https://events.test/";
    private static readonly Uri EventsRoot = new("https://events.test");

    private readonly FakeTokenIssuer issuer = new();
    private readonly EventsServiceHandler service = new();
    private readonly FakePerson person = new() { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Permissions = ["events:read"] };

    [Fact]
    public async Task SendRawAsync_ShouldSendAnExchangedToken_WhenAPersonIsOnTheCall()
    {
        await using var provider = Provider(options => options.Identity = Identity());
        PutPersonOnTheCall(provider);

        await Send(provider);

        var claims = Claims(service.Bearers.Single());
        claims.GetProperty("sub").GetString().Should().Be(person.Id.ToString());
        claims.GetProperty("act").GetProperty("sub").GetString().Should().Be(ClientId);
        claims.GetProperty("aud").GetString().Should().Be(EventsResource, "the resource defaults to the service root with its slash");
    }

    [Fact]
    public async Task SendRawAsync_ShouldSendAClientCredentialsToken_WhenNoPersonIsOnTheCall()
    {
        await using var provider = Provider(options => options.Identity = Identity());

        await Send(provider);

        var claims = Claims(service.Bearers.Single());
        claims.GetProperty("sub").GetString().Should().Be(ClientId);
        claims.GetProperty("aud").GetString().Should().Be(EventsResource);
        claims.TryGetProperty("act", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendRawAsync_ShouldSendTheServicesToken_UnderAsServiceEvenWithAPersonOnTheCall()
    {
        await using var provider = Provider(options => options.Identity = Identity());
        PutPersonOnTheCall(provider);

        using (EventsClientCalls.AsService())
            await Send(provider);
        await Send(provider);

        Claims(service.Bearers[0]).GetProperty("sub").GetString().Should().Be(ClientId);
        Claims(service.Bearers[1]).GetProperty("sub").GetString().Should().Be(person.Id.ToString(), "the scope ended");
    }

    [Fact]
    public async Task SendRawAsync_ShouldSendNoCredential_WithoutIdentity()
    {
        await using var provider = Provider(_ => { });
        PutPersonOnTheCall(provider);

        await Send(provider);

        service.Bearers.Single().Should().BeNull("a consumer without Identity adds its own token handler");
    }

    [Fact]
    public void Options_ShouldFailValidation_WhenIdentityNamesNoClient()
    {
        using var provider = Provider(options => options.Identity = Identity() with { ClientId = "" });

        FluentActions.Invoking(() => provider.GetRequiredService<IOptions<EventsClientOptions>>().Value)
            .Should().Throw<OptionsValidationException>()
            .WithMessage("*Identity.ClientId is required*");
    }

    public void Dispose() => issuer.Dispose();

    private ServiceProvider Provider(Action<EventsClientOptions> configure)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventsClient(options =>
            {
                options.BaseAddress = EventsRoot;
                configure(options);
            })
            .ConfigurePrimaryHttpMessageHandler(() => service);
        services.AddHttpClient(OAuth2HttpClientNames.TokenEndpoint).ConfigurePrimaryHttpMessageHandler(issuer.CreateTokenEndpointHandler);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static EventsClientIdentityOptions Identity() => new()
    {
        TokenEndpoint = new Uri("https://identity.test/connect/token"),
        ClientId = ClientId,
        ClientSecret = "test-secret",
    };

    /// <summary>Makes the current request carry the person's own token, as the calling service received it.</summary>
    private void PutPersonOnTheCall(IServiceProvider provider)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer " + issuer.IssuePersonToken(person, "https://desk.test/", ClientId);
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
    }

    private static async Task Send(IServiceProvider provider)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/catalogs");
        var sent = await provider.GetRequiredService<EventsApiTransport>().SendRawAsync(request, CancellationToken.None);
        sent.IsSuccess.Should().BeTrue();
        ((WoW.Two.Sdk.Beta.Be.Foundation.Results.Result<HttpResponseMessage>.Success)sent).Value.Dispose();
    }

    private static JsonElement Claims(string? bearer)
    {
        bearer.Should().NotBeNullOrEmpty();
        return JsonDocument.Parse(Base64Url.DecodeFromChars(bearer!.Split('.')[1])).RootElement;
    }

    /// <summary>Stands in for the Events service: records each call's bearer token and answers <c>204</c>.</summary>
    private sealed class EventsServiceHandler : HttpMessageHandler
    {
        public List<string?> Bearers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bearers.Add(request.Headers.Authorization is { Scheme: "Bearer" } header ? header.Parameter : null);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }
}
