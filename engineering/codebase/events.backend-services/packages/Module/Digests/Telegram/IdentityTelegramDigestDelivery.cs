using System.Globalization;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.Notices;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests.Telegram;

/// <summary>Delivers digests to people's linked Telegram accounts through Identity's app notices
/// (<see cref="DigestChannelConstants.Telegram"/>): the app never talks to the bot; Identity sends, once per idempotency key.</summary>
/// <remarks>
///   - Identity's answer is the recipient's result: <c>sent</c>, <c>muted</c> or <c>not_linked</c>; any failure reads
///     <c>failed</c> with its <c>identityCode</c>
///   - a rate limit, a send in progress and a bot that is down are retried after their <c>Retry-After</c>
/// </remarks>
/// <param name="notices">Identity's app notices: in process (Form 1) or the client over the instance (Forms 2 and 3).</param>
internal sealed class IdentityTelegramDigestDelivery(IAppNoticeBroker notices) : IDigestDelivery
{
    private static readonly TimeSpan DefaultRetry = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public string Channel => DigestChannelConstants.Telegram;

    /// <inheritdoc />
    public async Task<DigestDeliveryOutcome> DeliverAsync(DigestMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var result = await notices.SendNoticeAsync(
            new AppNotice
            {
                Subject = message.Subject,
                Text = message.Text,
                IdempotencyKey = message.IdempotencyKey,
                Culture = message.Language,
            },
            cancellationToken);

        return result switch
        {
            Result<AppNoticeDelivery>.Success { Value.Status: AppNoticeStatusConstants.Sent } => DigestDeliveryOutcome.Sent(),
            Result<AppNoticeDelivery>.Success { Value.Status: AppNoticeStatusConstants.Muted } => DigestDeliveryOutcome.Muted(),
            Result<AppNoticeDelivery>.Success { Value.Status: AppNoticeStatusConstants.NotLinked } => DigestDeliveryOutcome.NotLinked(),
            Result<AppNoticeDelivery>.Success { Value.Status: var status } => DigestDeliveryOutcome.Failed($"Identity answered an unknown status '{status}'"),
            Result<AppNoticeDelivery>.Failure { Error: var error } => Failed(error),
            _ => DigestDeliveryOutcome.Failed("Identity answered nothing"),
        };
    }

    private static DigestDeliveryOutcome Failed(AppError error)
    {
        var code = Metadata(error, AppNoticeConstants.CodeMetadataKey);
        var reason = Metadata(error, AppNoticeConstants.ReasonMetadataKey);
        var text = code is null ? $"{error.Type}: {error.Message}" : reason is null ? code : $"{code}: {reason}";
        return DigestDeliveryOutcome.Failed(text, RetryAfter(error, code));
    }

    private static TimeSpan? RetryAfter(AppError error, string? code)
    {
        var passing = code is AppNoticeErrorCodeConstants.RateLimited or AppNoticeErrorCodeConstants.InProgress or AppNoticeErrorCodeConstants.Unavailable
            || (code is null && error.Type is AppErrorType.ExternalUnavailable or AppErrorType.TooManyRequests or AppErrorType.OperationTimeout);
        if (!passing)
            return null;

        return int.TryParse(Metadata(error, AppNoticeConstants.RetryAfterMetadataKey), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : DefaultRetry;
    }

    private static string? Metadata(AppError error, string key) =>
        error.Metadata is { } metadata && metadata.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
}
