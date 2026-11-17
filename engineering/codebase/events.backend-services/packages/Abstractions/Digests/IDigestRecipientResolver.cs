using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Defines how a digest's role recipient becomes people: the members of an organization holding a role. The host
/// binds it to its Identity; with none bound, a role reads nothing and the run notes it.</summary>
public interface IDigestRecipientResolver
{
    /// <summary>Reads the <c>sub</c>s of an organization's members holding a role.</summary>
    /// <param name="issuer">The organization's issuer.</param>
    /// <param name="organizationId">The organization's id at its issuer.</param>
    /// <param name="role">The role, such as <c>member</c>.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The members' <c>sub</c>s; a failure when the role cannot be read.</returns>
    Task<Result<IReadOnlyList<string>>> ResolveRoleAsync(string issuer, string organizationId, string role, CancellationToken cancellationToken = default);
}
