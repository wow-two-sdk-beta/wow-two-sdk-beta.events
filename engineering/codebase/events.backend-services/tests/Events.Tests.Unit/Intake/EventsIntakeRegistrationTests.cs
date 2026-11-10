using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2ClientCredentials;

namespace Events.Tests.Unit.Intake;

/// <summary>Registering the intake from configuration: two sources, one token client, the hosted poller.</summary>
public sealed class EventsIntakeRegistrationTests
{
    [Fact]
    public void AddEventsIntake_ShouldBindTheSources_AndPutTheTokenClientOnTheirHttpClients()
    {
        var subscription = Guid.NewGuid();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Events:Intake:Sources:0:Name"] = "afisha-fashion",
            ["Events:Intake:Sources:0:Catalog"] = "ocharo-fashion",
            ["Events:Intake:Sources:0:BaseUrl"] = "https://harvest.test",
            ["Events:Intake:Sources:0:SubscriptionId"] = subscription.ToString("D").ToUpperInvariant(),
            ["Events:Intake:Sources:0:PollInterval"] = "00:00:30",
            ["Events:Intake:Sources:0:TokenEndpoint"] = "https://identity.test/connect/token",
            ["Events:Intake:Sources:0:ClientSecret"] = "test-secret",
            ["Events:Intake:Sources:1:Name"] = "afisha-exhibitions",
            ["Events:Intake:Sources:1:Catalog"] = "ocharo-fashion",
            ["Events:Intake:Sources:1:BaseUrl"] = "https://harvest.test",
            ["Events:Intake:Sources:1:SubscriptionId"] = Guid.NewGuid().ToString("D"),
        }).Build();
        var services = new ServiceCollection().AddLogging();

        services.AddEventsIntake(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<EventsIntakeOptions>>().Value;
        options.Sources.Select(source => source.Name).Should().Equal("afisha-fashion", "afisha-exhibitions");
        options.Sources[0].PollInterval.Should().Be(TimeSpan.FromSeconds(30));
        options.Sources[1].Instance.Should().Be("scfd");
        options.Sources[1].PageSize.Should().Be(100);
        IntakeSources.Subscription(options.Sources[0]).Should().Be(subscription.ToString("D"));

        var token = provider.GetRequiredService<IOptionsMonitor<OAuth2ClientCredentialsOptions>>().Get(IntakeSources.ClientName("afisha-fashion"));
        token.TokenEndpoint.Should().Be(new Uri("https://identity.test/connect/token"));
        token.ClientId.Should().Be("events");
        token.Scope.Should().Be("harvest.events-read");
        token.ClientSecret.Should().Be("test-secret");
        provider.GetRequiredService<IObservationFeed>().Should().BeOfType<ScfdObservationFeed>();
        provider.GetServices<IHostedService>().Should().ContainSingle().Which.Should().BeOfType<IntakePoller>();
    }
}
