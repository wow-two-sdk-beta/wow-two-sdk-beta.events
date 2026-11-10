using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Client;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;
using WoW.Two.Sdk.Beta.Be.Identity.Testing;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Events.Tests.Conformance.Client;

/// <summary>Forms 2 and 3: the Client wired as a consumer wires it, against the service host in process over a PostgreSQL
/// container. The host trusts a fake Identity issuer (<c>Events:Auth</c>); each actor's client sends that issuer's token —
/// a person's with every Events scope and permission in its organization, or a service's with every scope.</summary>
public sealed class ClientEventsFixture : IAsyncLifetime
{
    /// <summary>Holds the audience the host accepts: its own resource URI, with the slash.</summary>
    public const string Audience = "https://events.conformance.test/";

    /// <summary>Holds the app the person tokens are issued to.</summary>
    private const string AppClientId = "events-conformance-app";

    private static readonly string[] Scopes =
    [
        EventsScopeConstants.Read, EventsScopeConstants.Track, EventsScopeConstants.Review, EventsScopeConstants.Import,
        EventsScopeConstants.Admin,
    ];

    private static readonly string[] Permissions =
    [
        EventsPermissionConstants.Read, EventsPermissionConstants.Track, EventsPermissionConstants.Review,
        EventsPermissionConstants.Admin,
    ];

    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
    private readonly ConcurrentDictionary<EventsActor, ServiceProvider> clients = new();
    private WebApiTestHost<Program>? host;

    /// <summary>Gets the issuer the host trusts.</summary>
    public FakeTokenIssuer Issuer { get; } = new("https://identity.conformance.test/");

    /// <summary>Opens the brokers as <paramref name="actor"/> reaches them over HTTP.</summary>
    public EventsBrokerSet For(EventsActor actor)
    {
        var provider = clients.GetOrAdd(actor, Build);
        return new EventsBrokerSet(
            provider.GetRequiredService<IEventCatalogsBroker>(),
            provider.GetRequiredService<ICatalogEventsBroker>(),
            provider.GetRequiredService<IEventReviewBroker>(),
            provider.GetRequiredService<IEventTrackingBroker>(),
            provider.GetRequiredService<IEventWorkflowsBroker>(),
            provider.GetRequiredService<IEventImportsBroker>(),
            provider.GetRequiredService<IEventCalendarBroker>(),
            provider.GetRequiredService<IEventIntakeBroker>());
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await postgres.StartAsync();
        host = new WebApiTestHost<Program>
        {
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseSettings:ConnectionString"] = postgres.ConnectionString,
                ["Events:Auth:Audience"] = Audience,
                ["Events:Auth:Issuers:0:Issuer"] = Issuer.Issuer,
                ["Events:Auth:Issuers:0:Jwks"] = JwksOf(Issuer),
                ["Logging:LogLevel:Default"] = "Warning",
            }),
        };

        // The host stamps rows with its fake clock: let it tick from now, so rows written one after another (a tracking's
        // stage changes) carry distinct times, as they do on a real clock.
        host.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        host.Clock.AutoAdvanceAmount = TimeSpan.FromMilliseconds(1);

        // Building a client starts the host: a local run, so its boot applies the module's set.
        using var warmup = host.CreateClient();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        foreach (var provider in clients.Values)
            await provider.DisposeAsync();
        if (host is not null)
            await host.DisposeAsync();
        Issuer.Dispose();
        await postgres.DisposeAsync();
    }

    /// <summary>Writes an issuer's public key as the JSON Web Key Set a trusted issuer's settings take.</summary>
    public static string JwksOf(FakeTokenIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        var key = JsonWebKeyConverter.ConvertFromRSASecurityKey((RsaSecurityKey)issuer.AsTrustedIssuer().SigningKeys![0]);
        return $$"""{"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"{{key.Kid}}","n":"{{key.N}}","e":"{{key.E}}"}]}""";
    }

    private ServiceProvider Build(EventsActor actor)
    {
        var server = host ?? throw new InvalidOperationException("The fixture did not start.");
        var services = new ServiceCollection().AddLogging();
        services.AddEventsClient(options => options.BaseAddress = server.Server.BaseAddress)
            .AddHttpMessageHandler(() => new BearerTokenHandler(() => Token(actor)))
            .ConfigurePrimaryHttpMessageHandler(server.Server.CreateHandler);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private string Token(EventsActor actor) => actor.Kind == PrincipalKind.Service
        ? Issuer.IssueServiceToken(actor.Subject, Audience, Scopes)
        : Issuer.IssuePersonToken(
            new FakePerson
            {
                Id = Guid.Parse(actor.Subject),
                OrganizationId = Guid.Parse(actor.OrganizationId!),
                SessionId = Guid.NewGuid(),
                Role = "member",
                Permissions = Permissions,
            },
            Audience,
            AppClientId,
            Scopes);
}
