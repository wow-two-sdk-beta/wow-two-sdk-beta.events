namespace Events.Api.Auth;

/// <summary>Holds the review console's sign-in: the confidential client <c>events-console</c> at the catalog owner's
/// Identity instance, through Identity's BFF (session routes under <c>/api/identity</c>).</summary>
/// <remarks>Bound from <c>Events:Console</c> while the host composes, so a test host passes it as host configuration.
/// Without an authority and a client id the console pages still serve, and the API answers a browser 401.</remarks>
public sealed class EventsConsoleSettings
{
    /// <summary>Holds the section the settings bind from.</summary>
    public const string SectionName = "Events:Console";

    /// <summary>Holds the scopes the console asks for when <see cref="Scopes"/> is empty.</summary>
    public static readonly IReadOnlyList<string> DefaultScopes = ["openid", "profile", "events.read", "events.review", "events.admin"];

    /// <summary>Gets or sets the Identity instance's issuer URL, such as <c>https://id.example.com/</c>.</summary>
    public string? Authority { get; set; }

    /// <summary>Gets or sets the client id registered at the instance, <c>events-console</c>.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the client secret; a vault secret, never committed.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets the scopes to request; empty requests <see cref="DefaultScopes"/>.</summary>
    public List<string> Scopes { get; } = [];

    /// <summary>Gets the scopes to request: <see cref="Scopes"/>, or <see cref="DefaultScopes"/> when empty.</summary>
    public IReadOnlyList<string> EffectiveScopes => Scopes.Count > 0 ? Scopes : DefaultScopes;

    /// <summary>Gets whether the console signs people in: an absolute authority and a client id are set.</summary>
    public bool IsConfigured =>
        Uri.TryCreate(Authority, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(ClientId);
}
