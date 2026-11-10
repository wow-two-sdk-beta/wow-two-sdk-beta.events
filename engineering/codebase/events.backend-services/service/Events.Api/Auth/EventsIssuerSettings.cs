namespace Events.Api.Auth;

/// <summary>Holds one trusted issuer: the audiences its tokens may name and where its keys come from.</summary>
public sealed class EventsIssuerSettings
{
    /// <summary>Gets or sets the issuer, compared exactly with a token's <c>iss</c>.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Gets the audiences the issuer's tokens may name, each with its trailing slash; empty means
    /// <see cref="EventsAuthSettings.Audience"/>.</summary>
    public List<string> Audiences { get; } = [];

    /// <summary>Gets or sets the discovery address; null means <c>{Issuer}/.well-known/openid-configuration</c>.</summary>
    public Uri? MetadataAddress { get; set; }

    /// <summary>Gets or sets a JSON Web Key Set of the issuer's public keys, used instead of discovery: an issuer this host
    /// can't reach, or a test host.</summary>
    public string? Jwks { get; set; }
}
