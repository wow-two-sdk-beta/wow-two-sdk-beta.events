using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2;

namespace WoW.Two.Sdk.Beta.Be.Events.Client;

/// <summary>Holds the Identity client the Events client gets its tokens from, with the be sdk's own OAuth2 settings: the
/// token endpoint, the client id, <see cref="OAuth2TokenClientOptions.ClientSecret"/> or
/// <see cref="OAuth2TokenClientOptions.ClientPrivateKey"/>, the resource and the scopes.</summary>
/// <remarks>A null <see cref="OAuth2TokenClientOptions.Resource"/> asks for <see cref="EventsClientOptions.BaseAddress"/>
/// with its trailing slash, the audience Events accepts; a null <see cref="OAuth2TokenClientOptions.Scope"/> lets Identity
/// grant what the client may have. The be sdk handlers fetch, cache and renew every token; the Events client keeps none.</remarks>
public sealed record EventsClientIdentityOptions : OAuth2TokenClientOptions
{
    /// <summary>Copies these settings onto a be sdk grant's options, the resource defaulting to the service root.</summary>
    /// <param name="target">The grant's options.</param>
    /// <param name="baseAddress">The Events service's root URL.</param>
    internal void CopyTo(OAuth2TokenClientOptions target, Uri baseAddress)
    {
        target.TokenEndpoint = TokenEndpoint;
        target.ClientId = ClientId;
        target.ClientSecret = ClientSecret;
        target.ClientPrivateKey = ClientPrivateKey;
        target.ClientPrivateKeyId = ClientPrivateKeyId;
        target.ClientAssertionAudience = ClientAssertionAudience;
        target.Resource = string.IsNullOrWhiteSpace(Resource) ? EventsClientOptions.Root(baseAddress) : Resource;
        target.Scope = Scope;
        target.RefreshSkew = RefreshSkew;
        foreach (var parameter in AdditionalParameters)
            target.AdditionalParameters[parameter.Key] = parameter.Value;
    }
}
