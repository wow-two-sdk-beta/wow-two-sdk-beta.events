using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;
using WoW.Two.Sdk.Beta.Be.Events.Testing.Principals;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Events.Tests.Conformance.Module;

/// <summary>Form 1: the module wired as a consumer wires it, over one PostgreSQL container its own set builds; each actor
/// gets a scope whose <see cref="TestPrincipal"/> acts as that actor.</summary>
public sealed class ModuleEventsFixture : IAsyncLifetime
{
    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
    private readonly List<AsyncServiceScope> scopes = [];
    private ServiceProvider? services;

    /// <summary>Opens the brokers as <paramref name="actor"/> reaches them; the scope lives until the fixture ends.</summary>
    public EventsBrokerSet For(EventsActor actor)
    {
        var scope = (services ?? throw new InvalidOperationException("The fixture did not start.")).CreateAsyncScope();
        lock (scopes)
            scopes.Add(scope);

        var provider = scope.ServiceProvider;
        provider.GetRequiredService<TestPrincipal>().ActAs(actor);
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
        var collection = new ServiceCollection().AddLogging();
        collection.AddScoped<TestPrincipal>();
        collection.AddScoped<ICurrentPrincipal>(provider => provider.GetRequiredService<TestPrincipal>());
        collection.AddEvents().Configure(options => options.ConnectionString = postgres.ConnectionString);
        collection.AddEventsIntake(_ => { });
        services = collection.BuildServiceProvider(validateScopes: true);

        // The consumer's deploy step: every registered set, the module's included.
        (await services.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        foreach (var scope in scopes)
            await scope.DisposeAsync();
        if (services is not null)
            await services.DisposeAsync();
        await postgres.DisposeAsync();
    }
}
