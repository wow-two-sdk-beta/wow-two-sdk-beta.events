using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Observability.HealthChecks;
using WoW.Two.Sdk.Beta.Be.Events.Greetings;
using WoW.Two.Sdk.Beta.Be.Events.Module.Greetings;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

namespace WoW.Two.Sdk.Beta.Be.Events.Module;

/// <summary>Registers the in-process Events module (Form 1).</summary>
public static class EventsModuleServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IGreetingsBroker"/>, the module's store on its own keyed Npgsql data source, its migration
    /// set (<see cref="EventsModuleConstants.MigrationSetName"/>, in schema <see cref="EventsModuleConstants.SchemaName"/>)
    /// and the store's readiness check.</summary>
    /// <remarks>
    ///   - nothing it registers is unkeyed infrastructure, so it sits beside a host's own database and default set
    ///   - the host's migrate verbs and its boot helper (<c>MigrateBespokeOnStartupAsync</c>, which applies on a local run
    ///     only) reach the set with every other registered set
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="schemaOwner">The kind of host claiming the schema in the migrator's owner marker:
    /// <see cref="EventsModuleConstants.ModuleSchemaOwner"/> for a Form 1 product, <see cref="EventsModuleConstants.ServiceSchemaOwner"/>
    /// for the Events service. A schema claimed by one kind refuses the other until an audited <c>claim</c> moves it.</param>
    /// <returns>The options builder, through which the host sets the connection string; it is read on first use.</returns>
    public static OptionsBuilder<EventsModuleOptions> AddEvents(this IServiceCollection services, string schemaOwner = EventsModuleConstants.ModuleSchemaOwner)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaOwner);

        services.TryAddSingleton(TimeProvider.System);

        // The set's own database, so the be sdk's boot and deploy helpers create it when missing.
        services.AddKeyedSingleton(EventsModuleConstants.ServiceKey, (provider, _) =>
            new DatabaseSettings { ConnectionString = ConnectionString(provider) });

        // One pool for the context and the migration set, disposed with the container.
        services.AddKeyedSingleton(EventsModuleConstants.ServiceKey, (provider, _) => NpgsqlDataSource.Create(ConnectionString(provider)));
        services.AddKeyedSingleton<IDbConnectionFactory>(EventsModuleConstants.ServiceKey, (provider, _) =>
            new DataSourceConnectionFactory(provider.GetRequiredKeyedService<NpgsqlDataSource>(EventsModuleConstants.ServiceKey)));
        services.AddDbContext<EventsDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredKeyedService<NpgsqlDataSource>(EventsModuleConstants.ServiceKey))
            .UseSnakeCaseNamingConvention());

        // The schema is the module's: its embedded SQL, under its own history, lock and owner marker, in its own schema.
        services.AddDatabaseBespokeMigrationSet(EventsModuleConstants.MigrationSetName, typeof(EventsModuleAssembly).Assembly, migrations =>
        {
            migrations.Provider = DatabaseProvider.Postgres;
            migrations.SchemaName = EventsModuleConstants.SchemaName;
            migrations.SchemaOwner = schemaOwner;
        });

        services.AddScoped<IGreetingsBroker, ModuleGreetingsBroker>();
        services.AddHealthChecks().AddDatabaseReadinessCheck<EventsDbContext>();

        return services.AddOptions<EventsModuleOptions>()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "EventsModuleOptions.ConnectionString must be set.");
    }

    private static string ConnectionString(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<EventsModuleOptions>>().Value.ConnectionString;
}
