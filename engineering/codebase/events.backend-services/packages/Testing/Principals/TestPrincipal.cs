using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Principals;

/// <summary>A settable <see cref="ICurrentPrincipal"/> for tests: register it as a singleton, then switch who calls.</summary>
public sealed class TestPrincipal : ICurrentPrincipal
{
    /// <summary>Holds the issuer the helpers stamp.</summary>
    public const string DefaultIssuer = "https://identity.test/";

    /// <inheritdoc />
    public string? Subject { get; set; }

    /// <inheritdoc />
    public PrincipalKind Kind { get; set; } = PrincipalKind.Anonymous;

    /// <inheritdoc />
    public string? Issuer { get; set; }

    /// <inheritdoc />
    public string? ClientId { get; set; }

    /// <inheritdoc />
    public string? OrganizationId { get; set; }

    /// <inheritdoc />
    public IReadOnlySet<string> Roles { get; set; } = new HashSet<string>();

    /// <inheritdoc />
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();

    /// <inheritdoc />
    public IReadOnlySet<string> Scopes { get; set; } = new HashSet<string>();

    /// <inheritdoc />
    public ActorInfo? Actor { get; set; }

    /// <inheritdoc />
    public string? Partition { get; set; }

    /// <summary>Makes the principal a person of an organization holding every Events scope and permission.</summary>
    /// <param name="organizationId">The organization.</param>
    /// <param name="subject">The person.</param>
    /// <param name="issuer">The issuer; <see cref="DefaultIssuer"/> when none.</param>
    /// <returns>This principal.</returns>
    public TestPrincipal ActAsMember(string organizationId, string subject = "person-1", string? issuer = null)
    {
        Kind = PrincipalKind.Person;
        Issuer = issuer ?? DefaultIssuer;
        OrganizationId = organizationId;
        Subject = subject;
        ClientId = "test-app";
        Scopes = new HashSet<string>
        {
            EventsScopeConstants.Read, EventsScopeConstants.Track, EventsScopeConstants.Review, EventsScopeConstants.Import,
            EventsScopeConstants.Admin,
        };
        Permissions = new HashSet<string>
        {
            EventsPermissionConstants.Read, EventsPermissionConstants.Track, EventsPermissionConstants.Review,
            EventsPermissionConstants.Admin,
        };
        return this;
    }

    /// <summary>Makes the principal anonymous.</summary>
    /// <returns>This principal.</returns>
    public TestPrincipal ActAsAnonymous()
    {
        Kind = PrincipalKind.Anonymous;
        Issuer = OrganizationId = Subject = ClientId = null;
        Scopes = new HashSet<string>();
        Permissions = new HashSet<string>();
        return this;
    }
}
