namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Names what became of one recipient's message in a run, in the words of Identity's app notices.</summary>
public static class DigestRecipientStatusConstants
{
    /// <summary>The message waits to be sent, or to be retried after a passing failure.</summary>
    public const string Pending = "pending";

    /// <summary>The channel took the message.</summary>
    public const string Sent = "sent";

    /// <summary>The person has no account on the channel, such as no Telegram link; nothing went out.</summary>
    public const string NotLinked = "not_linked";

    /// <summary>The person muted the app's messages; nothing went out.</summary>
    public const string Muted = "muted";

    /// <summary>The channel failed, and no retry is left.</summary>
    public const string Failed = "failed";
}
