using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Reads the caller from the kernel's <see cref="ICurrentPrincipal"/>; with none registered, the caller is anonymous.</summary>
/// <param name="principal">The current principal, when the host registers one.</param>
internal sealed class PrincipalEventsCallerService(ICurrentPrincipal? principal = null) : IEventsCallerService
{
    /// <inheritdoc />
    public EventsCaller Current => principal is null || principal.Kind == PrincipalKind.Anonymous
        ? EventsCaller.Anonymous
        : new EventsCaller(principal.Kind, principal.Issuer ?? string.Empty, principal.OrganizationId, principal.Subject);
}
