using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2ClientCredentials;

namespace WoW.Two.Sdk.Beta.Be.Events.Module;

/// <summary>Registers the SCFD intake of the in-process Events module (events-integration #15, #19).</summary>
public static class EventsIntakeServiceCollectionExtensions
{
    /// <summary>Registers the intake from the <see cref="EventsIntakeOptions.SectionName"/> section: its settings, the SCFD
    /// reader with one named HTTP client per source (with the be sdk's client-credentials handler when the source names a
    /// token endpoint), the page processor and the hosted poller.</summary>
    /// <remarks>Call it beside <c>AddEvents</c>. Sources are read at registration to name their HTTP clients; their
    /// values (secrets included) are read when used.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The settings' builder.</returns>
    public static OptionsBuilder<EventsIntakeOptions> AddEventsIntake(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(EventsIntakeOptions.SectionName);
        return AddCore(services, section.Get<EventsIntakeOptions>() ?? new EventsIntakeOptions()).Bind(section);
    }

    /// <summary>Registers the intake with settings set in code; see the configuration overload.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the settings; it runs at registration and whenever the settings are read.</param>
    /// <returns>The settings' builder.</returns>
    public static OptionsBuilder<EventsIntakeOptions> AddEventsIntake(this IServiceCollection services, Action<EventsIntakeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new EventsIntakeOptions();
        configure(snapshot);
        return AddCore(services, snapshot).Configure(configure);
    }

    private static OptionsBuilder<EventsIntakeOptions> AddCore(IServiceCollection services, EventsIntakeOptions snapshot)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient();
        services.TryAddSingleton<IObservationFeed, ScfdObservationFeed>();
        services.TryAddScoped<IntakeProcessor>();
        services.TryAddSingleton<IntakeSourcePoller>();
        services.AddHostedService<IntakePoller>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EventsIntakeOptions>, EventsIntakeOptionsValidator>());

        foreach (var source in snapshot.Sources.Where(source => source is not null))
        {
            var name = IntakeSources.ClientName(source.Name);
            var client = services.AddHttpClient(name);
            if (string.IsNullOrWhiteSpace(source.TokenEndpoint))
                continue;

            var sourceName = source.Name;
            client.AddOAuth2ClientCredentials(_ => { });
            services.AddOptions<OAuth2ClientCredentialsOptions>(name).Configure<IOptionsMonitor<EventsIntakeOptions>>((token, intake) =>
            {
                var current = intake.CurrentValue.Sources.FirstOrDefault(item => item?.Name == sourceName);
                if (current is null || !Uri.TryCreate(current.TokenEndpoint, UriKind.Absolute, out var endpoint))
                    return;

                token.TokenEndpoint = endpoint;
                token.ClientId = current.ClientId;
                token.ClientSecret = current.ClientSecret ?? string.Empty;
                token.Scope = current.Scope;
                token.Resource = current.TokenResource();
            });
        }

        return services.AddOptions<EventsIntakeOptions>().ValidateOnStart();
    }
}
