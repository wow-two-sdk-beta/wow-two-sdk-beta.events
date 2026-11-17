using System.Globalization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.Members;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests.Identity;

/// <summary>Resolves a digest's role recipients through Identity's organization members (<see cref="IMemberBroker"/>), at each
/// run: <see cref="DigestRecipientRoleConstants.Member"/> reads every member, another role only its holders.</summary>
/// <remarks>
///   - every member reads, linked to the bot or not; the Telegram channel records <c>not_linked</c> for those Identity's
///     notices cannot reach, so the run shows who still has to link
///   - an organization of another issuer than <see cref="IdentityDigestRecipientOptions.Issuer"/>, an organization id that is
///     not a uuid, or Identity's refusal (its <c>identityCode</c> first) fails, and the run notes it
/// </remarks>
/// <param name="members">Identity's members: in process (Form 1) or the client over the instance (Forms 2 and 3).</param>
/// <param name="options">The bound Identity's issuer.</param>
internal sealed class IdentityMemberDigestRecipientResolver(IMemberBroker members, IOptions<IdentityDigestRecipientOptions> options) : IDigestRecipientResolver
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> ResolveRoleAsync(string issuer, string organizationId, string role, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (options.Value.Issuer is { Length: > 0 } bound && !string.Equals(Trimmed(bound), Trimmed(issuer), StringComparison.Ordinal))
            return Fail(AppErrorType.Validation, $"the organization's issuer {issuer} is not the bound Identity {bound}");
        if (!Guid.TryParse(organizationId, out var organization))
            return Fail(AppErrorType.Validation, $"the organization id {organizationId} is not an Identity organization id");

        var only = string.Equals(role.Trim(), DigestRecipientRoleConstants.Member, StringComparison.OrdinalIgnoreCase) ? null : role.Trim();
        var listed = await members.ListAsync(organization, only, cancellationToken);
        if (listed.IsFailure(out var error, out var found))
        {
            var code = error.Metadata is { } metadata && metadata.TryGetValue(MemberConstants.CodeMetadataKey, out var value) && value is not null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;
            return Fail(error.Type, code is null ? error.Message : $"{code}: {error.Message}");
        }

        IReadOnlyList<string> subs = [.. found!.Select(member => member.Subject)];
        return Result<IReadOnlyList<string>>.Ok(subs);
    }

    private static string Trimmed(string issuer) => issuer.Trim().TrimEnd('/');

    private static Result<IReadOnlyList<string>> Fail(AppErrorType type, string message) =>
        Result<IReadOnlyList<string>>.Fail(AppError.Of(type, message));
}
