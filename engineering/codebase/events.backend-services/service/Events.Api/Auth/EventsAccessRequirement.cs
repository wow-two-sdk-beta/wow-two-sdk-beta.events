using Microsoft.AspNetCore.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace Events.Api.Auth;

/// <summary>Requires what an Events action needs: the app's scope always, and a person's permission beside it (product §
/// Access). A service's own call (client credentials) holds no person, so its scope alone answers.</summary>
/// <param name="Scope">The scope the calling app must hold, such as <c>events.read</c>.</param>
/// <param name="Permission">The permission a person must hold in the active organization, such as <c>events:read</c>.</param>
public sealed record EventsAccessRequirement(string Scope, string Permission) : IAuthorizationRequirement
{
    /// <summary>Tells whether a principal may take the action; record rules (owner organization, visibility, tracking
    /// isolation) run later, in the module, against Events' own rows.</summary>
    /// <param name="principal">Who is calling.</param>
    /// <returns>Whether the gate lets the call through.</returns>
    public bool Allows(ICurrentPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.Kind switch
        {
            PrincipalKind.Service => principal.Scopes.Contains(Scope),
            PrincipalKind.Person => principal.Scopes.Contains(Scope) && principal.Permissions.Contains(Permission),
            _ => false,
        };
    }
}
