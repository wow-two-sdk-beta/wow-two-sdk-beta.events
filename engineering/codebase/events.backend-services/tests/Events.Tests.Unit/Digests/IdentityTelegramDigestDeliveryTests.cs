using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests.Telegram;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Identity.Notices;

namespace Events.Tests.Unit.Digests;

/// <summary>Covers the Telegram channel over Identity's app notices: Identity's answer becomes the recipient's result, and a
/// passing failure carries its retry.</summary>
public sealed class IdentityTelegramDigestDeliveryTests
{
    private readonly FakeAppNoticeBroker notices = new();

    [Fact]
    public async Task DeliverAsync_ShouldReadIdentitysAnswer()
    {
        var linked = Guid.NewGuid().ToString("D");
        var muted = Guid.NewGuid().ToString("D");
        notices.Link(linked).Link(muted).Mute(muted);
        var channel = new IdentityTelegramDigestDelivery(notices);

        (await channel.DeliverAsync(Message(linked))).Status.Should().Be(DigestRecipientStatusConstants.Sent);
        (await channel.DeliverAsync(Message(muted))).Status.Should().Be(DigestRecipientStatusConstants.Muted);
        (await channel.DeliverAsync(Message(Guid.NewGuid().ToString("D")))).Status.Should().Be(DigestRecipientStatusConstants.NotLinked);
        notices.Sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new AppNotice
        {
            Subject = linked,
            Text = "<b>Digest</b>",
            IdempotencyKey = $"events-digest:key:{linked}",
            Culture = "uz-Latn",
        });
    }

    [Theory]
    [InlineData(AppNoticeErrorCodeConstants.RateLimited, 42, 42)]
    [InlineData(AppNoticeErrorCodeConstants.Unavailable, null, 30)]
    [InlineData(AppNoticeErrorCodeConstants.InProgress, 3, 3)]
    [InlineData(AppNoticeErrorCodeConstants.Refused, null, null)]
    [InlineData(AppNoticeErrorCodeConstants.Invalid, null, null)]
    public async Task DeliverAsync_ShouldFail_WithARetryOnlyForAPassingFailure(string code, int? retryAfter, int? expectedSeconds)
    {
        var metadata = new Dictionary<string, object?> { [AppNoticeConstants.CodeMetadataKey] = code };
        if (retryAfter is { } seconds)
            metadata[AppNoticeConstants.RetryAfterMetadataKey] = seconds;
        notices.FailNext(AppError.Of(AppErrorType.ExternalRejected, "refused", metadata));

        var outcome = await new IdentityTelegramDigestDelivery(notices).DeliverAsync(Message(Guid.NewGuid().ToString("D")));

        outcome.Status.Should().Be(DigestRecipientStatusConstants.Failed);
        outcome.Error.Should().Be(code);
        outcome.RetryAfter.Should().Be(expectedSeconds is { } wait ? TimeSpan.FromSeconds(wait) : null);
    }

    [Fact]
    public async Task DeliverAsync_ShouldRetry_WhenIdentityIsUnreachable()
    {
        notices.FailNext(AppErrorFactory.ExternalUnavailable("Identity is unreachable."));

        var outcome = await new IdentityTelegramDigestDelivery(notices).DeliverAsync(Message(Guid.NewGuid().ToString("D")));

        outcome.Error.Should().Be("ExternalUnavailable: Identity is unreachable.");
        outcome.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
    }

    private static DigestMessage Message(string subject) => new()
    {
        DigestId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        Catalog = "ocharo-fashion",
        Issuer = "https://identity.test/",
        OrganizationId = "org",
        Subject = subject,
        Language = DigestLanguageConstants.UzbekLatin,
        Text = "<b>Digest</b>",
        IdempotencyKey = $"events-digest:key:{subject}",
    };
}
