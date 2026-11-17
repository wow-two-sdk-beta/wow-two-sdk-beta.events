using WoW.Two.Sdk.Beta.Be.Identity.Notices;

namespace Events.Api.Digests;

/// <summary>Holds the Telegram digest channel's link to Identity's app notices (<c>Events:Digests:Telegram</c>): the
/// instance and Events' own client there, which needs <see cref="AppNoticeConstants.Scope"/>.</summary>
public sealed class DigestTelegramSettings
{
    /// <summary>Holds the configuration section.</summary>
    public const string SectionName = "Events:Digests:Telegram";

    /// <summary>Gets or sets the Identity instance's address, such as its issuer; empty leaves the channel off.</summary>
    public string Identity { get; set; } = string.Empty;

    /// <summary>Gets or sets the instance's token endpoint.</summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets Events' client id at the instance.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets Events' client secret at the instance.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets the scope the token asks for.</summary>
    public string Scope { get; set; } = AppNoticeConstants.Scope;

    /// <summary>Gets or sets the resource the token is issued for: the instance's notice audience
    /// (Identity's <c>Identity:Notices:Audience</c>).</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>Gets whether the channel is on: the instance's address is set.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Identity);

    /// <summary>Lists what is missing or invalid while the channel is on.</summary>
    /// <returns>The problems; empty when the channel is off or complete.</returns>
    public IReadOnlyList<string> Problems()
    {
        if (!IsConfigured)
            return [];

        var problems = new List<string>();
        if (!IsAbsolute(Identity))
            problems.Add($"{SectionName}:Identity must be an absolute https or http URL.");
        if (!IsAbsolute(TokenEndpoint))
            problems.Add($"{SectionName}:TokenEndpoint must be an absolute https or http URL.");
        if (string.IsNullOrWhiteSpace(ClientId))
            problems.Add($"{SectionName}:ClientId is required.");
        if (string.IsNullOrWhiteSpace(ClientSecret))
            problems.Add($"{SectionName}:ClientSecret is required.");
        if (!IsAbsolute(Resource))
            problems.Add($"{SectionName}:Resource must be the instance's notice audience, an absolute URL.");
        return problems;
    }

    private static bool IsAbsolute(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp);
}
