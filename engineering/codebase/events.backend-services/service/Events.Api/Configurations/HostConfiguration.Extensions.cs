using Events.Api.Auth;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Web.DataProtection;
using WoW.Two.Sdk.Beta.Be.Web.Json;

namespace Events.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Hosts the Form 1 module over the service's PostgreSQL database, with its own migration set and the SCFD intake.</summary>
    private static WebApplicationBuilder AddEventsModule(this WebApplicationBuilder builder)
    {
        // Resolved on first use, so a test host's configuration overrides still reach the connection string.
        builder.Services.AddEvents(EventsModuleConstants.ServiceSchemaOwner)
            .Configure<IConfiguration>((options, configuration) => options.ConnectionString = ResolveConnectionString(configuration));

        // The SCFD pull: one poller per source of Events:Intake:Sources; none configured polls nothing.
        builder.Services.AddEventsIntake(builder.Configuration);
        return builder;
    }

    /// <summary>Keeps the console's sign-in sessions across restarts and deploys: Identity's BFF stores them in
    /// <c>IDistributedCache</c>, here a table in the service's own database (the be sdk's <c>distributed_cache</c> set,
    /// registered always so every boot hosts the same sets), and their cookies are protected with a key ring kept in
    /// <c>DataProtection:KeysPath</c> (the deploy's keys volume) under <c>DataProtection:ApplicationName</c>; a local run
    /// keeps its machine folder. The key ring registers only with the console.</summary>
    private static WebApplicationBuilder AddConsoleStorage(this WebApplicationBuilder builder)
    {
        builder.Services.AddPostgresDistributedCache();
        builder.Services.AddOptions<PostgresDistributedCacheOptions>()
            .Configure<IConfiguration>((options, configuration) => options.ConnectionString = ConnectionStringOrEmpty(configuration));

        var console = builder.Configuration.GetSection(EventsConsoleSettings.SectionName).Get<EventsConsoleSettings>();
        if (console?.IsConfigured != true)
            return builder;

        builder.Services.AddPersistentDataProtection(_ => { });
        builder.Services.AddOptions<PersistentDataProtectionOptions>()
            .Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            {
                options.ApplicationName = configuration["DataProtection:ApplicationName"] is { Length: > 0 } name
                    ? name
                    : $"events:{environment.EnvironmentName}";
                options.KeyDirectory = configuration["DataProtection:KeysPath"] is { Length: > 0 } path
                    ? path
                    : environment.IsLocalRun(configuration)
                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "events", environment.EnvironmentName, "keys")
                        : null;
            });
        return builder;
    }

    /// <summary>Registers controllers on the SDK wire contract.</summary>
    private static WebApplicationBuilder AddControllers(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllersWithSdkJson();
        return builder;
    }

    /// <summary>Resolves the connection string: the <c>DB_CONNECTION</c> alias wins over <c>DatabaseSettings:ConnectionString</c>.</summary>
    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var connectionString = ConnectionStringOrEmpty(configuration);
        return connectionString.Length == 0
            ? throw new InvalidOperationException("Database connection string not found. Set DB_CONNECTION or DatabaseSettings:ConnectionString.")
            : connectionString;
    }

    /// <summary>Reads the connection string, or empty when none is set (describing the migration sets needs none).</summary>
    private static string ConnectionStringOrEmpty(IConfiguration configuration) =>
        configuration["DB_CONNECTION"] is { Length: > 0 } alias ? alias : configuration["DatabaseSettings:ConnectionString"] ?? string.Empty;
}
