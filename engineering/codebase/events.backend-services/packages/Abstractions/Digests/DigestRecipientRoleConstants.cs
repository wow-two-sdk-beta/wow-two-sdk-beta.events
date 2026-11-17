namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Names the role words a digest's <see cref="DigestRecipient.Role"/> takes beyond the organization's own roles.</summary>
public static class DigestRecipientRoleConstants
{
    /// <summary>Every member of the digest's organization, whatever their role (<c>role:member</c> in the editor). Identity's
    /// catalog has no role of this name; any other role, such as <c>owner</c> or <c>editor</c>, reads only its holders.</summary>
    public const string Member = "member";
}
