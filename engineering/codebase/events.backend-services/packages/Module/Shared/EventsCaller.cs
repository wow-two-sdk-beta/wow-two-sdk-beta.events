using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Represents who calls a broker: an organization at an issuer, and the person or service acting.</summary>
/// <param name="Kind">How the principal is identified.</param>
/// <param name="Issuer">The issuer; empty in process, where a Form 1 host names none.</param>
/// <param name="OrganizationId">The active organization; none for a service or a person without one.</param>
/// <param name="Subject">The person's or client's id.</param>
internal sealed record EventsCaller(PrincipalKind Kind, string Issuer, string? OrganizationId, string? Subject)
{
    /// <summary>Gets the anonymous caller.</summary>
    public static EventsCaller Anonymous { get; } = new(PrincipalKind.Anonymous, string.Empty, null, null);

    /// <summary>Gets whether the caller acts for an organization.</summary>
    public bool HasOrganization => !string.IsNullOrEmpty(OrganizationId);

    /// <summary>Tells whether the caller acts for the given organization.</summary>
    public bool IsOrganization(string issuer, string organizationId) =>
        HasOrganization && string.Equals(Issuer, issuer, StringComparison.Ordinal) && string.Equals(OrganizationId, organizationId, StringComparison.Ordinal);
}
