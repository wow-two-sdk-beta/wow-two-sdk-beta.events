using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Client.Greetings;
using WoW.Two.Sdk.Beta.Be.Events.Greetings;

namespace WoW.Two.Sdk.Beta.Be.Events.Client;

/// <summary>Registers the Events HTTP client (Forms 2 and 3).</summary>
public static class EventsClientServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IGreetingsBroker"/> over the running Events service.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the service's root URL.</param>
    /// <returns>The HTTP client builder, for resilience handlers or a test server's handler.</returns>
    public static IHttpClientBuilder AddEventsClient(this IServiceCollection services, Action<EventsClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<EventsClientOptions>()
            .Configure(configure)
            .Validate(options => options.BaseAddress is { IsAbsoluteUri: true }, "EventsClientOptions.BaseAddress must be an absolute URL.");

        return services.AddHttpClient<IGreetingsBroker, ApiGreetingsBroker>((provider, http) =>
        {
            var root = provider.GetRequiredService<IOptions<EventsClientOptions>>().Value.BaseAddress!.ToString();
            http.BaseAddress = new Uri(root.EndsWith('/') ? root : root + "/");
        });
    }
}
