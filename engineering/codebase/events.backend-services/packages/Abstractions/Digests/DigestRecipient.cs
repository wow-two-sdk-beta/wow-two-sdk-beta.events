namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Names who receives a digest: one person by their Identity <c>sub</c>, or every member of the digest's
/// organization holding a role.</summary>
/// <remarks>Exactly one of <see cref="Subject"/> and <see cref="Role"/> is set. A role resolves through the host's
/// <see cref="IDigestRecipientResolver"/>; a person named twice receives one message, in the language of the first entry.</remarks>
public sealed record DigestRecipient
{
    /// <summary>Gets the person's <c>sub</c> at the organization's issuer.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the organization role whose members receive it, such as <c>owner</c> or <c>member</c>.</summary>
    public string? Role { get; init; }

    /// <summary>Gets the language this recipient reads (<see cref="DigestLanguageConstants"/>); none takes the digest's.</summary>
    public string? Language { get; init; }
}
