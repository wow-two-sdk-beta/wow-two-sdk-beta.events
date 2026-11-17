using AwesomeAssertions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests.Identity;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.Members;

namespace Events.Tests.Unit.Digests;

/// <summary>Covers role recipients over Identity's organization members: <c>member</c> reads everyone, another role its
/// holders, and a refusal, another issuer or a foreign organization id reads nothing with the reason.</summary>
public sealed class IdentityMemberDigestRecipientResolverTests
{
    private const string Issuer = "https://identity.test/";
    private static readonly Guid Organization = Guid.NewGuid();
    private readonly FakeMemberBroker members = new();

    [Fact]
    public async Task ResolveRoleAsync_ShouldReadEveryMember_ForMember_AndOnlyTheHolders_ForARole()
    {
        var (owner, editor, unlinked) = ("owner-sub", "editor-sub", "unlinked-sub");
        members.AddMember(Organization, owner, "owner").AddMember(Organization, editor, "editor").AddMember(Organization, unlinked, "editor", hasTelegramLink: false);
        var resolver = Resolver(Issuer);

        Subs(await resolver.ResolveRoleAsync("https://identity.test", Organization.ToString("D"), "Member")).Should().Equal(owner, editor, unlinked);
        Subs(await resolver.ResolveRoleAsync(Issuer, Organization.ToString("D"), "editor")).Should().Equal(editor, unlinked);
        members.Asked.Should().Equal((Organization, (string?)null), (Organization, "editor"));
    }

    [Fact]
    public async Task ResolveRoleAsync_ShouldFailWithTheReason_AndAskNothing_ForAnotherIssuerOrAForeignOrganizationId()
    {
        var resolver = Resolver(Issuer);

        Message(await resolver.ResolveRoleAsync("https://other.test/", Organization.ToString("D"), "member")).Should().Contain("is not the bound Identity");
        Message(await resolver.ResolveRoleAsync(Issuer, "org-ocharo", "member")).Should().Contain("is not an Identity organization id");
        members.Asked.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveRoleAsync_ShouldCarryIdentitysCode_WhenItRefuses()
    {
        members.AddOrganization(Organization);
        members.FailNext(AppError.Of(AppErrorType.ExternalUnavailable, "The Identity instance could not be reached.", new Dictionary<string, object?>
        {
            [MemberConstants.CodeMetadataKey] = MemberConstants.UnavailableCode,
        }));
        var resolver = Resolver(issuer: null);

        Message(await resolver.ResolveRoleAsync(Issuer, Organization.ToString("D"), "member")).Should().StartWith("MembersUnavailable: ");
        Message(await resolver.ResolveRoleAsync(Issuer, Guid.NewGuid().ToString("D"), "member")).Should().StartWith("OrganizationNotFound: ");
        Message(await resolver.ResolveRoleAsync(Issuer, Organization.ToString("D"), "staff")).Should().StartWith("UnknownOrganizationRole: ");
    }

    private IdentityMemberDigestRecipientResolver Resolver(string? issuer) =>
        new(members, Options.Create(new IdentityDigestRecipientOptions { Issuer = issuer }));

    private static IReadOnlyList<string> Subs(Result<IReadOnlyList<string>> result) =>
        result is Result<IReadOnlyList<string>>.Success { Value: var subs } ? subs : throw new InvalidOperationException("The role was not read.");

    private static string Message(Result<IReadOnlyList<string>> result) =>
        result is Result<IReadOnlyList<string>>.Failure { Error: var error } ? error.Message : throw new InvalidOperationException("The role was read.");
}
