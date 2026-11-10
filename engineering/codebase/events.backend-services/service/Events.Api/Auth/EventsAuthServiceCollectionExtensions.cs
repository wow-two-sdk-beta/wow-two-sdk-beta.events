using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentUser;
using WoW.Two.Sdk.Beta.Be.Identity.Jwt;
using WoW.Two.Sdk.Beta.Be.Identity.Signals;

namespace Events.Api.Auth;

/// <summary>Registers how Events API callers authenticate, what each action needs, and how revoked tokens stop counting.</summary>
public static class EventsAuthServiceCollectionExtensions
{
    /// <summary>Holds each policy: its name, the scope the app needs, and the permission a person needs beside it
    /// (contract § HTTP mirror).</summary>
    private static readonly (string Policy, string Scope, string Permission)[] Policies =
    [
        (EventsScopeConstants.Read, EventsScopeConstants.Read, EventsPermissionConstants.Read),
        (EventsScopeConstants.Track, EventsScopeConstants.Track, EventsPermissionConstants.Track),
        (EventsScopeConstants.Review, EventsScopeConstants.Review, EventsPermissionConstants.Review),
        (EventsScopeConstants.Import, EventsScopeConstants.Import, EventsPermissionConstants.Admin),
        (EventsScopeConstants.Admin, EventsScopeConstants.Admin, EventsPermissionConstants.Admin),
        (EventsAuthConstants.WorkflowWritePolicy, EventsScopeConstants.Track, EventsPermissionConstants.Admin),
    ];

    /// <summary>Registers Identity's token scheme over the trusted issuers of <c>Events:Auth</c>, the review console's BFF
    /// session when <c>Events:Console</c> names an authority and a client id, the policy scheme that picks between them,
    /// the one principal, a policy per action, and Identity's revocation signals with the deny list.</summary>
    /// <remarks>
    ///   - tokens: issuers are read on use (<see cref="EventsIssuerResolver"/>), each with its audiences, else
    ///     <c>Events:Auth:Audience</c>
    ///   - console: decided while the host composes, from the configuration it has then; a test host passes
    ///     <c>Events:Console</c> as host configuration
    ///   - no API keys: every caller holds an Identity token, or the console's session
    /// </remarks>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static WebApplicationBuilder AddEventsAuth(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;
        var isDevelopment = builder.Environment.IsDevelopment();

        services.Configure<EventsAuthSettings>(builder.Configuration.GetSection(EventsAuthSettings.SectionName));

        // Tokens: the resolver reads each issuer from the settings when first seen, with its own audiences.
        services.AddJwtBearerAuthentication(options =>
        {
            options.Audience = EventsAuthConstants.UnsetAudience;
            options.IsInsecureMetadataAllowedInDevelopment = isDevelopment;
            options.UseTrustedIssuerResolver<EventsIssuerResolver>();
        });

        // The console: Identity's BFF as confidential client, its session a person principal beside the tokens.
        var console = builder.Configuration.GetSection(EventsConsoleSettings.SectionName).Get<EventsConsoleSettings>() ?? new EventsConsoleSettings();
        var isConsoleEnabled = console.IsConfigured;
        services.AddSingleton(new EventsConsoleState(isConsoleEnabled));
        if (isConsoleEnabled)
            AddConsole(services, console, builder.Configuration[$"{EventsAuthSettings.SectionName}:{nameof(EventsAuthSettings.Audience)}"]);

        // Registered after the bearer and the BFF, so this default wins over theirs: one scheme per request, by its credential.
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = EventsAuthConstants.Scheme;
                options.DefaultAuthenticateScheme = EventsAuthConstants.Scheme;
                options.DefaultChallengeScheme = EventsAuthConstants.Scheme;
                options.DefaultForbidScheme = EventsAuthConstants.Scheme;
            })
            .AddPolicyScheme(EventsAuthConstants.Scheme, "Events", options =>
                options.ForwardDefaultSelector = context => SelectScheme(context, isConsoleEnabled));

        services.AddCurrentPrincipal();
        services.AddSingleton<IAuthorizationHandler, EventsAccessHandler>();
        services.AddAuthorization(options =>
        {
            foreach (var (policy, scope, permission) in Policies)
                options.AddPolicy(policy, rule => rule.RequireAuthenticatedUser().AddRequirements(new EventsAccessRequirement(scope, permission)));
        });

        // Revocation: one route under each audience hears its issuer; a signal refuses that person's tokens until they expire.
        services.AddIdentitySignalReceiver();
        services.AddOptions<IdentitySignalReceiverOptions>()
            .Configure<IOptionsMonitor<EventsAuthSettings>>((options, settings) => ConfigureSignals(options, settings.CurrentValue, isDevelopment));
        services.AddTokenDenyList();

        return builder;
    }

    /// <summary>Adds authentication, the console session's antiforgery check when the console signs in, and authorization.</summary>
    /// <param name="app">The pipeline, after routing.</param>
    /// <returns>The same pipeline.</returns>
    public static IApplicationBuilder UseEventsAuth(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.ApplicationServices.GetRequiredService<EventsConsoleState>().IsEnabled)
            return app.UseIdentityBff();

        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>Maps the signal route when the settings name an issuer and an audience, and the console's session routes
    /// (<c>/api/identity/session</c>, <c>/sign-in</c>, <c>/sign-out</c>, <c>/callback</c>, <c>/signal</c>) when it signs in.</summary>
    /// <param name="app">The built application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication MapEventsAuth(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var signals = app.Services.GetRequiredService<IOptions<IdentitySignalReceiverOptions>>().Value;
        if (signals.TrustedIssuers.Count > 0 && signals.Audiences.Count > 0)
            app.MapIdentitySignals(EventsAuthConstants.SignalPath);
        if (app.Services.GetRequiredService<EventsConsoleState>().IsEnabled)
            app.MapIdentityBffEndpoints();
        return app;
    }

    /// <summary>Lists every issuer's keys and every audience's signal URL, <c>{audience}signals/identity</c>.</summary>
    internal static void ConfigureSignals(IdentitySignalReceiverOptions options, EventsAuthSettings settings, bool isDevelopment)
    {
        options.IsInsecureMetadataAllowedInDevelopment |= isDevelopment;
        foreach (var issuer in settings.Issuers)
        {
            if (EventsIssuerResolver.ToTrustedIssuer(issuer, settings) is not { } trusted)
                continue;

            options.TrustedIssuers.Add(trusted with { Audiences = [] });
            foreach (var audience in trusted.Audiences)
            {
                var url = UnderAudience(audience, EventsRouteConstants.Signals);
                if (!options.Audiences.Contains(url, StringComparer.Ordinal))
                    options.Audiences.Add(url);
            }
        }
    }

    /// <summary>Builds a URL under a resource URI, which keeps its trailing slash.</summary>
    internal static string UnderAudience(string audience, string path) =>
        (audience.EndsWith('/') ? audience : audience + "/") + path;

    /// <summary>Registers the console's sign-in through Identity's BFF (default route prefix <c>/api/identity</c>) and the
    /// claims its session lacks beside a token's.</summary>
    private static void AddConsole(IServiceCollection services, EventsConsoleSettings console, string? audience)
    {
        var authority = new Uri(console.Authority!, UriKind.Absolute);
        services.AddIdentityBff(bff =>
        {
            bff.Authority = console.Authority!;
            bff.ClientId = console.ClientId!;
            bff.ClientSecret = string.IsNullOrWhiteSpace(console.ClientSecret) ? null : console.ClientSecret;
            if (console.Scopes.Count > 0)
                bff.Scopes = [.. console.Scopes];
            bff.CookieName = ".events.session";
            bff.IsHttpsMetadataRequired = !(authority.Scheme == Uri.UriSchemeHttp && authority.IsLoopback);
            if (bff.SignalUrl is null && !string.IsNullOrWhiteSpace(audience))
                bff.SignalUrl = UnderAudience(audience, EventsAuthConstants.ConsoleSignalPath);
        });
        services.AddSingleton(console);
        services.AddSingleton<IClaimsTransformation, EventsConsoleClaimsTransformation>();
    }

    /// <summary>Picks the token scheme for a bearer token, the console's session for any other request while the console
    /// signs in, and the token scheme otherwise, which answers a bare 401.</summary>
    private static string SelectScheme(HttpContext context, bool isConsoleEnabled)
    {
        var isBearer = context.Request.Headers.Authorization.FirstOrDefault() is { } authorization
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        return isBearer || !isConsoleEnabled ? JwtBearerDefaults.AuthenticationScheme : BffAuthenticationSchemeConstants.Cookie;
    }
}
