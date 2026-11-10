using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Principals;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Events.Tests.Integration.Intake;

/// <summary>The Form 1 module with its SCFD intake (<c>AddEventsIntake</c>), fed by a fake feed instead of SCFD, over
/// one PostgreSQL container.</summary>
/// <remarks>A copy of <c>ModuleFixture</c> plus the intake: each test isolates itself with its own catalog, source name
/// and subscription. The hosted poller is registered but never started; tests poll on demand.</remarks>
public sealed class IntakeFixture : IAsyncLifetime
{
    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
    private readonly List<EventsIntakeSourceOptions> sources = [];

    /// <summary>Gets the clock the module stamps rows with.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

    /// <summary>Gets who calls: switch it per step.</summary>
    public TestPrincipal Principal { get; } = new();

    /// <summary>Gets the consumer's container.</summary>
    public ServiceProvider Services { get; private set; } = null!;

    /// <summary>Gets the feed records come from.</summary>
    internal FakeObservationFeed Feed { get; } = new();

    /// <summary>Gets the seam that crashes a page just before its commit.</summary>
    internal CrashingCommitHook Crash { get; } = new();

    /// <summary>Opens a scope to resolve brokers in.</summary>
    public AsyncServiceScope Scope() => Services.CreateAsyncScope();

    /// <summary>Configures a source delivering into a catalog; the intake's settings read it from the next scope on.</summary>
    internal EventsIntakeSourceOptions AddSource(string catalog, Action<EventsIntakeSourceOptions>? configure = null)
    {
        var source = new EventsIntakeSourceOptions
        {
            Name = $"src-{Guid.NewGuid():N}",
            Catalog = catalog,
            BaseUrl = "https://scfd.test/",
            SubscriptionId = Guid.NewGuid().ToString("D"),
            PipelineId = Guid.NewGuid().ToString("D"),
            // The SCFD filter the subscription carries; the fake subscription matches it unless a test sets another.
            Filter = "fixture-filter",
            PageSize = 50,
            MaxPagesPerPoll = 5,
            // The catalog's owner: the organization acting when the test created it.
            OwnerOrg = Principal.OrganizationId,
            OwnerIssuer = Principal.Issuer,
        };
        configure?.Invoke(source);
        lock (sources)
            sources.Add(source);
        return source;
    }

    /// <summary>Polls one source now, as the hosted poller does.</summary>
    internal Task PollAsync(EventsIntakeSourceOptions source) =>
        Services.GetRequiredService<IntakeSourcePoller>().PollAsync(source, CancellationToken.None);

    async Task IAsyncLifetime.InitializeAsync()
    {
        await postgres.StartAsync();
        var services = new ServiceCollection().AddLogging()
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<ICurrentPrincipal>(Principal);
        services.AddEvents().Configure(options => options.ConnectionString = postgres.ConnectionString);
        services.AddEventsIntake(options =>
        {
            lock (sources)
            {
                foreach (var source in sources)
                    options.Sources.Add(source);
            }
        });
        services.AddSingleton<IObservationFeed>(Feed);
        services.AddSingleton<IIntakeCommitHook>(Crash);
        Services = services.BuildServiceProvider(validateScopes: true);

        (await Services.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Services.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
