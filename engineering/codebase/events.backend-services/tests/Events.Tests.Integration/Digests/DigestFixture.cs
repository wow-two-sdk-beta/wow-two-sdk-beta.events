using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Principals;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;
using WoW.Two.Sdk.Beta.Be.Identity.Notices;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Events.Tests.Integration.Digests;

/// <summary>One PostgreSQL container the module's set builds once; each test starts its own hosts over it
/// (<see cref="StartHost"/>), each with the test's fake clock and Identity's fake app notices, so a second host over the
/// same database is a restart.</summary>
public sealed class DigestFixture : IAsyncLifetime
{
    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());

    /// <summary>Gets the database's connection string.</summary>
    public string ConnectionString => postgres.ConnectionString;

    /// <summary>Starts a host: the module and the digests' scheduler wired as a consumer wires them, sending Telegram
    /// digests through <paramref name="notices"/>.</summary>
    /// <param name="clock">The host's clock.</param>
    /// <param name="notices">Identity's app notices.</param>
    /// <param name="configure">Registers more, such as a role resolver.</param>
    /// <returns>The host's container.</returns>
    public DigestHost StartHost(FakeTimeProvider clock, FakeAppNoticeBroker notices, Action<IServiceCollection>? configure = null)
    {
        var principal = new TestPrincipal();
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<TimeProvider>(clock)
            .AddSingleton<ICurrentPrincipal>(principal)
            .AddSingleton<IAppNoticeBroker>(notices);
        services.AddEvents().Configure(options => options.ConnectionString = postgres.ConnectionString);
        services.AddEventDigests(options => options.LinkTemplates["linked-catalog"] = "https://app.example.test/events/{id}");
        services.AddIdentityTelegramDigestDelivery();
        configure?.Invoke(services);
        return new DigestHost(services.BuildServiceProvider(validateScopes: true), principal, clock, notices);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await postgres.StartAsync();
        var services = new ServiceCollection().AddLogging();
        services.AddEvents().Configure(options => options.ConnectionString = postgres.ConnectionString);
        await using var provider = services.BuildServiceProvider();
        (await provider.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }

    async Task IAsyncLifetime.DisposeAsync() => await postgres.DisposeAsync();
}
