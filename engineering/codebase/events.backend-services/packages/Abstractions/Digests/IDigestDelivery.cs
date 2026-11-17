namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Defines a channel a digest goes out through: Telegram through Identity's app notices, the host's log, and
/// email or webhooks behind the same seam. The host registers one per channel; a digest names its channel.</summary>
/// <remarks>
///   - a run sends each recipient's message once per <see cref="DigestMessage.IdempotencyKey"/>: a retry after a crash
///     or a passing failure repeats the key, and the channel must send nothing more
///   - a channel answers every outcome, its failures included; an exception counts as a failure with no retry
/// </remarks>
public interface IDigestDelivery
{
    /// <summary>Gets the channel's name, matching <see cref="DigestChannelConstants.Pattern"/>, such as
    /// <see cref="DigestChannelConstants.Telegram"/>.</summary>
    string Channel { get; }

    /// <summary>Sends one recipient's message.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The token that cancels the send.</param>
    /// <returns>What became of it.</returns>
    Task<DigestDeliveryOutcome> DeliverAsync(DigestMessage message, CancellationToken cancellationToken = default);
}
