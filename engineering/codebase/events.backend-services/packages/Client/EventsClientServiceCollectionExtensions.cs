using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2ClientCredentials;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2TokenExchange;
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
    /// (<see cref="EventsClientConstants.HttpClientName"/>) pointed at <see cref="EventsClientOptions.BaseAddress"/>; with
    /// <see cref="EventsClientOptions.Identity"/> set, each call first takes an Identity token.</summary>
    /// <remarks>
    ///   - with Identity, a call exchanges the person's token when <see cref="IOAuth2SubjectTokenSource"/> finds a person
    ///     on it (the current request's bearer token by default; the signed-in session's under the Identity BFF), and sends
    ///     the service's client-credentials token otherwise or under <see cref="EventsClientCalls.AsService"/>; both ask
    ///     for the service root with its trailing slash as the resource, so a consumer writes no grant options
    ///   - without Identity, the client sends no credential of its own: chain a token handler on the returned builder
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
            .Validate(options => options.BaseAddress is { IsAbsoluteUri: true }, "EventsClientOptions.BaseAddress must be an absolute URL.")
            .Validate(options => options.Identity is null or { TokenEndpoint.IsAbsoluteUri: true }, "EventsClientOptions.Identity.TokenEndpoint must be an absolute URL.")
            .Validate(options => options.Identity is null || !string.IsNullOrWhiteSpace(options.Identity.ClientId), "EventsClientOptions.Identity.ClientId is required.")
            .Validate(
                options => options.Identity is null
                    || string.IsNullOrWhiteSpace(options.Identity.ClientSecret) != string.IsNullOrWhiteSpace(options.Identity.ClientPrivateKey),
                "EventsClientOptions.Identity: set ClientSecret or ClientPrivateKey, exactly one.");

        services.AddTransient<EventsApiTransport>();
        services.AddTransient<IEventCatalogsBroker, ApiEventCatalogsBroker>();
        services.AddTransient<ICatalogEventsBroker, ApiCatalogEventsBroker>();
        services.AddTransient<IEventReviewBroker, ApiEventReviewBroker>();
        services.AddTransient<IEventTrackingBroker, ApiEventTrackingBroker>();
        services.AddTransient<IEventWorkflowsBroker, ApiEventWorkflowsBroker>();
        services.AddTransient<IEventImportsBroker, ApiEventImportsBroker>();
        services.AddTransient<IEventCalendarBroker, ApiEventCalendarBroker>();
        services.AddTransient<IEventIntakeBroker, ApiEventIntakeBroker>();

        // The token clients read the Identity settings when first built; without Identity they are never created.
        services.AddOptions<OAuth2ClientCredentialsOptions>(EventsClientConstants.ServiceHttpClientName)
            .Configure<IOptions<EventsClientOptions>>((target, events) => events.Value.Identity?.CopyTo(target, events.Value.BaseAddress!));
        services.AddOptions<OAuth2TokenExchangeOptions>(EventsClientConstants.PersonHttpClientName)
            .Configure<IOptions<EventsClientOptions>>((target, events) => events.Value.Identity?.CopyTo(target, events.Value.BaseAddress!));

        AddTokenClient(services, EventsClientConstants.ServiceHttpClientName).AddOAuth2ClientCredentials(_ => { });
        AddTokenClient(services, EventsClientConstants.PersonHttpClientName).AddOAuth2TokenExchange(_ => { });

        return services.AddHttpClient(EventsClientConstants.HttpClientName, SetRoot);
    }

    /// <summary>Registers a token client: the service root, and the Events client's own pipeline after its token handler.</summary>
    private static IHttpClientBuilder AddTokenClient(IServiceCollection services, string name) =>
        services.AddHttpClient(name, SetRoot)
            .ConfigurePrimaryHttpMessageHandler(provider => new EventsPipelineHandler(provider.GetRequiredService<IHttpMessageHandlerFactory>()));

    /// <summary>Points a client at the service root, with its trailing slash.</summary>
    private static void SetRoot(IServiceProvider provider, HttpClient http) =>
        http.BaseAddress = new Uri(EventsClientOptions.Root(provider.GetRequiredService<IOptions<EventsClientOptions>>().Value.BaseAddress!));
}
