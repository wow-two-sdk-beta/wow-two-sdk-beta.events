using WoW.Two.Sdk.Beta.Be.Events;

namespace Events.Api.Auth;

/// <summary>Holds the review console's sign-in: the confidential client <c>events-console</c> at the catalog owner's
/// Identity instance, through Identity's BFF (session routes under <c>/api/identity</c>).</summary>
/// <remarks>Bound from <c>Events:Console</c> while the host composes, so a test host passes it as host configuration.
/// Without an authority and a client id the console pages still serve, and the API answers a browser 401.</remarks>
public sealed class EventsConsoleSettings
{
    /// <summary>Holds the section the settings bind from.</summary>
    public const string SectionName = "Events:Console";

    /// <summary>Holds the API scopes a console session holds. The console is Events' own app, served by the same host, so
    /// its session needs no token for Events: the session carries these scopes, and the person's <c>events:*</c>
    /// permissions from the sign-in gate each action, as on a token (events-integration #23).</summary>
    public static readonly IReadOnlyList<string> ApiScopes =
    [
        EventsScopeConstants.Read, EventsScopeConstants.Review, EventsScopeConstants.Track, EventsScopeConstants.Import,
        EventsScopeConstants.Admin,
    ];

    /// <summary>Gets or sets the Identity instance's issuer URL, such as <c>https://id.example.com/</c>.</summary>
    public string? Authority { get; set; }

    /// <summary>Gets or sets the client id registered at the instance, <c>events-console</c>.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the client secret; a vault secret, never committed.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets the scopes the sign-in requests; empty requests Identity BFF's defaults (<c>openid profile email
    /// phone offline_access</c>). Never <c>events.*</c>: Identity grants those only through token exchange, and a
    /// sign-in asking for them is refused.</summary>
    public List<string> Scopes { get; } = [];

    /// <summary>Gets whether the console signs people in: an absolute authority and a client id are set.</summary>
    public bool IsConfigured =>
        Uri.TryCreate(Authority, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(ClientId);
}
