namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests.Identity;

/// <summary>Holds the settings of digest role recipients read from Identity's organization members.</summary>
public sealed class IdentityDigestRecipientOptions
{
    /// <summary>Gets or sets the issuer of the Identity instance the member broker reads, such as <c>https://id.example/</c>;
    /// a digest of another issuer's organization reads no role and its run notes why. Empty takes every issuer.</summary>
    public string? Issuer { get; set; }
}
