using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Identity.Testing;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using WoW.Two.Sdk.Beta.Be.Testing.MultiHost;

namespace Events.Tests.E2E;

/// <summary>Boots the real API over an isolated PostgreSQL container, trusting a fake Identity issuer for one audience;
/// the test host is a local run, so its boot applies the SQL migrations. A second host over the same database composes
/// the review console's BFF sign-in as well (<c>Events:Console</c>, passed as host configuration).</summary>
public sealed class EventsApiFixture : MultiHostFixture, IAsyncLifetime
{
    /// <summary>Holds the public host the test target allows, beside <c>localhost</c>, as a deployed target's <c>AllowedHosts</c> does.</summary>
    public const string PublicHost = "events.example.test";

    /// <summary>Holds the audience the API accepts: its own resource URI, with the slash.</summary>
    public const string Audience = "https://events.example.test/";

    /// <summary>Holds the console's Identity instance; never called, since no test signs in through it.</summary>
    public const string ConsoleAuthority = "https://identity.console.test/";

    private readonly PostgresFixture postgres;

    public EventsApiFixture()
    {
        postgres = AddSharedFixture(new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build()));
        Host = AddHost(new WebApiTestHost<Program>
        {
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(Settings()),
        });
        ConsoleHost = AddHost(new WebApiTestHost<Program>
        {
            // The console is decided while the host composes, so its settings travel as host configuration.
            ConfigureHostHook = host => host.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Events:Console:Authority"] = ConsoleAuthority,
                ["Events:Console:ClientId"] = "events-console",
                ["Events:Console:ClientSecret"] = "console-test-secret",
            })),
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(Settings()),
        });
    }

    /// <summary>Gets the issuer the API trusts.</summary>
    public FakeTokenIssuer Issuer { get; } = new("https://identity.events.test/");

    /// <summary>Gets the booted API host.</summary>
    public WebApiTestHost<Program> Host { get; }

    /// <summary>Gets the booted API host whose review console signs in through Identity's BFF.</summary>
    public WebApiTestHost<Program> ConsoleHost { get; }

    /// <summary>Writes an issuer's public key as the JSON Web Key Set a trusted issuer's settings take.</summary>
    public static string JwksOf(FakeTokenIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        var key = JsonWebKeyConverter.ConvertFromRSASecurityKey((RsaSecurityKey)issuer.AsTrustedIssuer().SigningKeys![0]);
        return $$"""{"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"{{key.Kid}}","n":"{{key.N}}","e":"{{key.E}}"}]}""";
    }

    Task IAsyncLifetime.InitializeAsync() => StartAsync().AsTask();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        Issuer.Dispose();
    }

    private Dictionary<string, string?> Settings() => new()
    {
        ["DatabaseSettings:ConnectionString"] = postgres.ConnectionString,
        ["AllowedHosts"] = PublicHost + ";localhost",
        ["Events:Auth:Audience"] = Audience,
        ["Events:Auth:Issuers:0:Issuer"] = Issuer.Issuer,
        ["Events:Auth:Issuers:0:Jwks"] = JwksOf(Issuer),
        ["Logging:LogLevel:Default"] = "Warning",
    };
}

/// <summary>Shares one <see cref="EventsApiFixture"/> across the whole E2E run.</summary>
[CollectionDefinition(Name)]
public sealed class EventsApiCollection : ICollectionFixture<EventsApiFixture>
{
    /// <summary>The collection name every E2E class joins.</summary>
    public const string Name = "events-api";
}
