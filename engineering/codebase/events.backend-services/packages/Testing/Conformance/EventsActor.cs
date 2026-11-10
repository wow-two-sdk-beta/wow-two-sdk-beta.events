using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>Represents who calls in a conformance case: a person of an organization holding every Events scope and
/// permission, or a service with every scope and no organization. Ids are GUIDs, as Identity's tokens carry them.</summary>
public sealed record EventsActor
{
    /// <summary>Gets how the actor is identified: <see cref="PrincipalKind.Person"/> or <see cref="PrincipalKind.Service"/>.</summary>
    public required PrincipalKind Kind { get; init; }

    /// <summary>Gets the person's active organization; none for a service.</summary>
    public string? OrganizationId { get; init; }

    /// <summary>Gets the person's id, or the service's client id.</summary>
    public required string Subject { get; init; }

    /// <summary>Returns a person of <paramref name="organizationId"/>.</summary>
    /// <param name="organizationId">The organization, a GUID in <c>D</c> format.</param>
    /// <param name="subject">The person, a GUID in <c>D</c> format; a new one when none.</param>
    /// <returns>The actor.</returns>
    public static EventsActor Member(string organizationId, string? subject = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        return new EventsActor { Kind = PrincipalKind.Person, OrganizationId = organizationId, Subject = subject ?? Guid.NewGuid().ToString("D") };
    }

    /// <summary>Returns a service acting as itself (client credentials), with no organization.</summary>
    /// <param name="clientId">The service's client id.</param>
    /// <returns>The actor.</returns>
    public static EventsActor Service(string clientId = "events-conformance") =>
        new() { Kind = PrincipalKind.Service, Subject = clientId };

    /// <summary>Returns a fresh organization id.</summary>
    /// <returns>A GUID in <c>D</c> format.</returns>
    public static string NewOrganization() => Guid.NewGuid().ToString("D");
}
