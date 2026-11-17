using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests.Telegram;

namespace WoW.Two.Sdk.Beta.Be.Events.Module;

/// <summary>Registers the scheduler of the in-process Events module's digests (V07).</summary>
public static class EventsDigestsServiceCollectionExtensions
{
    /// <summary>Registers the hosted scheduler with its settings from <see cref="EventDigestsOptions.SectionName"/>: it
    /// starts each due occurrence once and resumes runs a dead host left.</summary>
    /// <remarks>Call it beside <c>AddEvents</c>, which registers the digests broker and the <c>log</c> channel; without it a
    /// digest still previews and sends now.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The settings' builder.</returns>
    public static OptionsBuilder<EventDigestsOptions> AddEventDigests(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return AddCore(services).Bind(configuration.GetSection(EventDigestsOptions.SectionName));
    }

    /// <summary>Registers the hosted scheduler with settings set in code; see the configuration overload.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the settings.</param>
    /// <returns>The settings' builder.</returns>
    public static OptionsBuilder<EventDigestsOptions> AddEventDigests(this IServiceCollection services, Action<EventDigestsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = AddCore(services);
        return configure is null ? builder : builder.Configure(configure);
    }

    /// <summary>Registers the <see cref="DigestChannelConstants.Telegram"/> channel: digests reach people's linked Telegram
    /// accounts through Identity's app notices.</summary>
    /// <remarks>The host registers Identity's <c>IAppNoticeBroker</c>: in process beside Identity's module, or over an
    /// instance with <c>services.AddIdentityAppNotices(instance).AddOAuth2ClientCredentials(…)</c> from Identity's client
    /// package, whose client needs the <c>identity.notices.send</c> scope.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddIdentityTelegramDigestDelivery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDigestDelivery, IdentityTelegramDigestDelivery>());
        return services;
    }

    private static OptionsBuilder<EventDigestsOptions> AddCore(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<DigestSchedulerPass>();
        services.AddHostedService<DigestScheduler>();
        return services.AddOptions<EventDigestsOptions>()
            .Validate(options => options.PollInterval > TimeSpan.Zero, "Events:Digests:PollInterval must be positive.")
            .Validate(options => options.CatchUpWindow >= TimeSpan.Zero, "Events:Digests:CatchUpWindow must not be negative.")
            .Validate(options => options.LeaseDuration > TimeSpan.Zero, "Events:Digests:LeaseDuration must be positive.")
            .Validate(options => options.MaxAttempts is >= 1 and <= 50, "Events:Digests:MaxAttempts must be 1 to 50.")
            .Validate(
                options => options.LinkTemplates.Values.All(template => string.IsNullOrWhiteSpace(template) || DigestSpecValidator.IsLinkTemplate(template.Trim())),
                "Events:Digests:LinkTemplates: each value must be empty or an absolute https or http URL template.")
            .ValidateOnStart();
    }
}
