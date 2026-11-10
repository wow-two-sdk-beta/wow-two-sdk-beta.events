using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Beta.Be.Events.Client;

/// <summary>Registers the Events HTTP client (Forms 2 and 3). STUB: replaced by its implementation.</summary>
public static class EventsClientServiceCollectionExtensions
{
    /// <summary>Registers the Events brokers over the running Events service.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the service's root URL.</param>
    /// <returns>The HTTP client builder.</returns>
    public static IHttpClientBuilder AddEventsClient(this IServiceCollection services, Action<EventsClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<EventsClientOptions>().Configure(configure);
        return services.AddHttpClient("events");
    }
}
