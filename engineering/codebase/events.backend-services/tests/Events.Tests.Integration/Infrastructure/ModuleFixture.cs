using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Principals;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Events.Tests.Integration.Infrastructure;

/// <summary>The Form 1 module wired as a consumer wires it, over one PostgreSQL container its own set builds.</summary>
/// <remarks>Shared by a test class (<c>IClassFixture</c>); each test isolates itself with its own catalog slug and
/// organization ids (<see cref="NewSlug"/>, <see cref="NewOrganization"/>), so no reset runs between tests.</remarks>
public sealed class ModuleFixture : IAsyncLifetime
{
    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());

    /// <summary>Gets the clock the module stamps rows with.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

    /// <summary>Gets who calls: switch it per step.</summary>
    public TestPrincipal Principal { get; } = new();

    /// <summary>Gets the database's connection string.</summary>
    public string ConnectionString => postgres.ConnectionString;

    /// <summary>Gets the consumer's container.</summary>
    public ServiceProvider Services { get; private set; } = null!;

    /// <summary>Gets a fresh catalog slug.</summary>
    public static string NewSlug(string prefix = "catalog") => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(64, prefix.Length + 33)];

    /// <summary>Gets a fresh organization id.</summary>
    public static string NewOrganization() => Guid.NewGuid().ToString("N");

    /// <summary>Opens a scope to resolve brokers in.</summary>
    public AsyncServiceScope Scope() => Services.CreateAsyncScope();

    /// <summary>Registers extra services before the container builds; set before the fixture initializes.</summary>
    public Action<IServiceCollection>? Configure { get; set; }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await postgres.StartAsync();
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<ICurrentPrincipal>(Principal);
        services.AddEvents().Configure(options => options.ConnectionString = postgres.ConnectionString);
        Configure?.Invoke(services);
        Services = services.BuildServiceProvider(validateScopes: true);

        // The consumer's deploy step: every registered set, the module's included.
        (await Services.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
