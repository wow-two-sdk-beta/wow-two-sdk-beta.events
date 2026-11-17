using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Beta.Be.Events.Digests;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Delivers digests to the host's log only (<see cref="DigestChannelConstants.Log"/>): every message reads
/// <c>sent</c>. For local runs and tests.</summary>
/// <param name="logger">The log.</param>
internal sealed partial class LogDigestDelivery(ILogger<LogDigestDelivery> logger) : IDigestDelivery
{
    /// <inheritdoc />
    public string Channel => DigestChannelConstants.Log;

    /// <inheritdoc />
    public Task<DigestDeliveryOutcome> DeliverAsync(DigestMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        LogDelivered(logger, message.DigestId, message.RunId, message.Subject, message.Language, message.Text);
        return Task.FromResult(DigestDeliveryOutcome.Sent());
    }

    [LoggerMessage(EventId = 4300, Level = LogLevel.Information, Message = "Events digest {DigestId} run {RunId} to {Subject} ({Language}):\n{Text}")]
    private static partial void LogDelivered(ILogger logger, Guid digestId, Guid runId, string subject, string language, string text);
}
