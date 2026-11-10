using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Client.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.Client.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Client.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Client.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Client.Review;
using WoW.Two.Sdk.Beta.Be.Events.Client.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Events.Client.Workflows;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;

namespace WoW.Two.Sdk.Beta.Be.Events.Client;

/// <summary>Registers the Events HTTP client (Forms 2 and 3).</summary>
public static class EventsClientServiceCollectionExtensions
{
    /// <summary>Registers every Events broker over the running Events service, through one named <see cref="HttpClient"/>
    /// (<see cref="EventsClientConstants.HttpClientName"/>) pointed at <see cref="EventsClientOptions.BaseAddress"/>.</summary>
    /// <remarks>
    ///   - the client sends no credential of its own: chain the be sdk's <c>AddOAuth2TokenExchange</c> (acting for the
    ///     person on the current call) or <c>AddOAuth2ClientCredentials</c> (acting as the service) on the returned builder,
    ///     with the service root and its trailing slash as the resource
    ///   - a failure comes back as an <c>AppError</c> by the answer's status: 400 <c>Validation</c>, 401 <c>Unauthorized</c>,
    ///     403 <c>Forbidden</c>, 404 <c>NotFound</c>, 409 <c>Conflict</c>; any other status, and an unreachable service,
    ///     <c>ExternalUnavailable</c>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the service's root URL.</param>
    /// <returns>The HTTP client builder every call goes through, for the token handler, resilience handlers or a test
    /// server's handler.</returns>
    public static IHttpClientBuilder AddEventsClient(this IServiceCollection services, Action<EventsClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<EventsClientOptions>()
            .Configure(configure)
            .Validate(options => options.BaseAddress is { IsAbsoluteUri: true }, "EventsClientOptions.BaseAddress must be an absolute URL.");

        services.AddTransient<EventsApiTransport>();
        services.AddTransient<IEventCatalogsBroker, ApiEventCatalogsBroker>();
        services.AddTransient<ICatalogEventsBroker, ApiCatalogEventsBroker>();
        services.AddTransient<IEventReviewBroker, ApiEventReviewBroker>();
        services.AddTransient<IEventTrackingBroker, ApiEventTrackingBroker>();
        services.AddTransient<IEventWorkflowsBroker, ApiEventWorkflowsBroker>();
        services.AddTransient<IEventImportsBroker, ApiEventImportsBroker>();
        services.AddTransient<IEventCalendarBroker, ApiEventCalendarBroker>();
        services.AddTransient<IEventIntakeBroker, ApiEventIntakeBroker>();

        return services.AddHttpClient(EventsClientConstants.HttpClientName, static (provider, http) =>
            http.BaseAddress = new Uri(EventsClientOptions.Root(provider.GetRequiredService<IOptions<EventsClientOptions>>().Value.BaseAddress!)));
    }
}
