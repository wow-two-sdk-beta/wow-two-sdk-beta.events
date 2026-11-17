namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents what a channel did with one message (<see cref="IDigestDelivery"/>).</summary>
public sealed record DigestDeliveryOutcome
{
    /// <summary>Gets the result (<see cref="DigestRecipientStatusConstants"/>): <c>sent</c>, <c>not_linked</c>, <c>muted</c>
    /// or <c>failed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Gets why it failed, such as the channel's error code; none otherwise.</summary>
    public string? Error { get; init; }

    /// <summary>Gets how long to wait before a retry may pass, for a passing failure (a rate limit, a channel that is down);
    /// none when a retry would fail the same way.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Returns a sent message.</summary>
    public static DigestDeliveryOutcome Sent() => new() { Status = DigestRecipientStatusConstants.Sent };

    /// <summary>Returns a message to a person with no account on the channel.</summary>
    public static DigestDeliveryOutcome NotLinked() => new() { Status = DigestRecipientStatusConstants.NotLinked };

    /// <summary>Returns a message to a person who muted the app.</summary>
    public static DigestDeliveryOutcome Muted() => new() { Status = DigestRecipientStatusConstants.Muted };

    /// <summary>Returns a failed message.</summary>
    /// <param name="error">Why, such as the channel's error code.</param>
    /// <param name="retryAfter">How long to wait before a retry may pass; none when a retry would fail the same way.</param>
    public static DigestDeliveryOutcome Failed(string error, TimeSpan? retryAfter = null) =>
        new() { Status = DigestRecipientStatusConstants.Failed, Error = error, RetryAfter = retryAfter };
}
