using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace Events.Api.Provisioning;

/// <summary>Acts for an owner organization from the host itself, as the deploy's operator: no person, no token.</summary>
/// <param name="Issuer">The organization's issuer.</param>
/// <param name="OrganizationId">The organization's id at its issuer.</param>
public sealed record OperatorPrincipal(string Issuer, string OrganizationId) : ICurrentPrincipal
{
    /// <summary>Holds the subject the operator's writes record.</summary>
    public const string OperatorSubject = "events-operator";

    /// <inheritdoc />
    public string? Subject => OperatorSubject;

    /// <inheritdoc />
    public PrincipalKind Kind => PrincipalKind.Service;

    /// <inheritdoc />
    string? ICurrentPrincipal.Issuer => Issuer;

    /// <inheritdoc />
    public string? ClientId => OperatorSubject;

    /// <inheritdoc />
    string? ICurrentPrincipal.OrganizationId => OrganizationId;

    /// <inheritdoc />
    public IReadOnlySet<string> Roles { get; } = new HashSet<string>();

    /// <inheritdoc />
    public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();

    /// <inheritdoc />
    public IReadOnlySet<string> Scopes { get; } = new HashSet<string>();

    /// <inheritdoc />
    public ActorInfo? Actor => null;

    /// <inheritdoc />
    public string? Partition => null;
}
