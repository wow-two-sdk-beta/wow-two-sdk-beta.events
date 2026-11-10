using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Core;

namespace Events.Api.Auth;

/// <summary>Completes a console session's principal as a token would carry it: the console's authority as <c>iss</c>, which
/// the session's ID-token claims leave out, and the scopes the console signed in with as <c>scope</c>. The one principal
/// then reads a session like a token, so the module keys its rows by (<c>iss</c>, <c>org_id</c>) either way and the
/// access policies never branch by scheme.</summary>
/// <param name="console">The console's settings, as the host composed them.</param>
/// <param name="auth">The trusted issuers, whose spelling of the authority wins.</param>
internal sealed class EventsConsoleClaimsTransformation(EventsConsoleSettings console, IOptionsMonitor<EventsAuthSettings> auth) : IClaimsTransformation
{
    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var index = principal.Identities.ToList().FindIndex(identity =>
            identity.IsAuthenticated && identity.AuthenticationType == BffAuthenticationSchemeConstants.Cookie);
        if (index < 0 || principal.Identities.ElementAt(index).HasClaim(claim => claim.Type == ServiceClaimTypeConstants.Scope))
            return Task.FromResult(principal);

        var completed = principal.Clone();
        var identity = completed.Identities.ElementAt(index);
        if (identity.FindFirst(JwtRegisteredClaimNames.Iss) is null)
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, Issuer()));
        identity.AddClaim(new Claim(ServiceClaimTypeConstants.Scope, string.Join(' ', console.EffectiveScopes)));
        return Task.FromResult(completed);
    }

    /// <summary>Names the console's authority as a trusted issuer spells it (with or without its trailing slash), else as configured.</summary>
    private string Issuer()
    {
        var authority = console.Authority ?? string.Empty;
        var trimmed = authority.TrimEnd('/');
        return auth.CurrentValue.Issuers
            .Select(issuer => issuer.Issuer)
            .FirstOrDefault(issuer => string.Equals(issuer.TrimEnd('/'), trimmed, StringComparison.Ordinal))
            ?? authority;
    }
}
