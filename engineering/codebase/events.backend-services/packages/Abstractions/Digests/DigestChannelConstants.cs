namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Names the channels a digest goes out through; each is an <see cref="IDigestDelivery"/> the host registers.</summary>
public static class DigestChannelConstants
{
    /// <summary>The person's linked Telegram account, through Identity's app notices and its bot.</summary>
    public const string Telegram = "telegram";

    /// <summary>The host's log only: for local runs and tests.</summary>
    public const string Log = "log";

    /// <summary>Holds the pattern of a channel's name.</summary>
    public const string Pattern = "^[a-z][a-z0-9-]{0,31}$";
}
