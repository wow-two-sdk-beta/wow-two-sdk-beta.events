using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Stands in until the host binds <see cref="IDigestRecipientResolver"/> to its Identity: no role resolves, and
/// each run notes the role it could not read.</summary>
internal sealed class UnboundDigestRecipientResolver : IDigestRecipientResolver
{
    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> ResolveRoleAsync(string issuer, string organizationId, string role, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<IReadOnlyList<string>>.Fail(
            AppErrorFactory.ExternalUnavailable("no recipient resolver is bound to the organization's Identity; name people by sub")));
}
