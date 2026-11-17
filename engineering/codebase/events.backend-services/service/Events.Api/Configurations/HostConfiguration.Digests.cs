using Events.Api.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2ClientCredentials;
using WoW.Two.Sdk.Beta.Be.Identity.Members;
using WoW.Two.Sdk.Beta.Be.Identity.Notices;

namespace Events.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Turns the Telegram digest channel on when <c>Events:Digests:Telegram:Identity</c> names an instance: Identity's
    /// app notice client over it, with Events' own client-credentials token for the instance's notice audience, and the
    /// module's channel over that client; and Identity's member client with its own token (<c>MembersScope</c>, the same
    /// resource), so a role recipient such as <c>role:member</c> reads the organization's members at each run. Off, a Telegram
    /// digest's recipients read <c>failed</c> and a role reads nothing; the log channel stays.</summary>
    private static WebApplicationBuilder AddTelegramDigests(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var settings = configuration.GetSection(DigestTelegramSettings.SectionName).Get<DigestTelegramSettings>() ?? new DigestTelegramSettings();
        builder.Services.AddOptions<DigestTelegramSettings>()
            .Bind(configuration.GetSection(DigestTelegramSettings.SectionName))
            .Validate(current => current.Problems().Count == 0, "Events:Digests:Telegram is incomplete: Identity, TokenEndpoint, ClientId, ClientSecret and Resource.")
            .ValidateOnStart();
        if (!settings.IsConfigured || !Uri.TryCreate(settings.Identity, UriKind.Absolute, out var instance))
            return builder;

        builder.Services.AddIdentityAppNotices(instance).AddOAuth2ClientCredentials(token =>
        {
            var current = configuration.GetSection(DigestTelegramSettings.SectionName).Get<DigestTelegramSettings>() ?? new DigestTelegramSettings();
            if (Uri.TryCreate(current.TokenEndpoint, UriKind.Absolute, out var endpoint))
                token.TokenEndpoint = endpoint;
            token.ClientId = current.ClientId;
            token.ClientSecret = current.ClientSecret;
            token.Scope = string.IsNullOrWhiteSpace(current.Scope) ? AppNoticeConstants.Scope : current.Scope;
            token.Resource = current.Resource;
        });
        builder.Services.AddIdentityTelegramDigestDelivery();

        builder.Services.AddIdentityMembers(instance).AddOAuth2ClientCredentials(token =>
        {
            var current = configuration.GetSection(DigestTelegramSettings.SectionName).Get<DigestTelegramSettings>() ?? new DigestTelegramSettings();
            if (Uri.TryCreate(current.TokenEndpoint, UriKind.Absolute, out var endpoint))
                token.TokenEndpoint = endpoint;
            token.ClientId = current.ClientId;
            token.ClientSecret = current.ClientSecret;
            token.Scope = string.IsNullOrWhiteSpace(current.MembersScope) ? MemberConstants.Scope : current.MembersScope;
            token.Resource = current.Resource;
        });
        builder.Services.AddIdentityDigestRecipients(recipients => recipients.Issuer = settings.Identity);
        return builder;
    }
}
