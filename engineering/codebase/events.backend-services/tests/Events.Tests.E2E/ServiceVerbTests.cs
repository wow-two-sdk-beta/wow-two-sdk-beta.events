using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Events.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration.ConfigCheck;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace Events.Tests.E2E;

/// <summary>The image's verbs through the be sdk's <see cref="ServiceHost"/> (the services sweep): Events' operator verbs
/// run before any host boots, and <c>config check</c> runs the boot's settings validation without serving, as the
/// deploy's config check step does.</summary>
public sealed class ServiceVerbTests
{
    [Theory]
    [InlineData("event-show-result ok=false reason=usage", "event", "show")]
    [InlineData("import-result ok=false reason=usage", "import")]
    [InlineData("catalog-ensure-result ok=false reason=usage", "catalog", "ensure")]
    public async Task ServiceHost_ShouldDispatchTheOperatorVerbs_BeforeAnyHost(string last, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await ServiceHost.RunAsync(args, builder => builder.Configure(), app => app.ConfigureAsync(), verbs => verbs.AddEventsVerbs(), output, error, CancellationToken.None);

        exit.Should().Be(1);
        output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[^1].Should().Be(last);
    }

    [Fact]
    public async Task ConfigCheck_ShouldPass_OnConsistentSettings()
    {
        var (exit, last, _) = await ConfigCheckAsync(Source());

        exit.Should().Be(0, last);
        last.Should().StartWith("config-result ").And.Contain("\"exit\":0");
    }

    /// <summary>An automatic source with the filter classifier names its subscription's filter, or the deploy stops at the
    /// config check, before the restart.</summary>
    [Fact]
    public async Task ConfigCheck_ShouldRefuse_AnAutomaticSourceWithoutItsFilter()
    {
        var settings = Source();
        settings.Remove("Events:Intake:Sources:0:Filter");

        var (exit, last, report) = await ConfigCheckAsync(settings);

        exit.Should().Be(1, last);
        (last + report).Should().Contain("Filter must name the subscription's SCFD filter");
    }

    private static Dictionary<string, string?> Source() => new()
    {
        ["Events:Intake:Sources:0:Name"] = "afisha-fashion",
        ["Events:Intake:Sources:0:Catalog"] = "ocharo-fashion",
        ["Events:Intake:Sources:0:Instance"] = "scfd",
        ["Events:Intake:Sources:0:BaseUrl"] = "https://scfd.test/",
        ["Events:Intake:Sources:0:SubscriptionId"] = "0193f3a4-0000-7000-8000-000000000001",
        ["Events:Intake:Sources:0:TokenEndpoint"] = "https://identity.test/connect/token",
        ["Events:Intake:Sources:0:ClientId"] = "events",
        ["Events:Intake:Sources:0:ClientSecret"] = "secret",
        ["Events:Intake:Sources:0:Review"] = "auto",
        ["Events:Intake:Sources:0:Classifier"] = "filter",
        ["Events:Intake:Sources:0:Filter"] = "ocharo-fashion-v1",
        ["Events:Intake:Sources:0:OwnerOrg"] = "org-ocharo",
    };

    private static async Task<(int Exit, string Last, string Report)> ConfigCheckAsync(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(settings)
        {
            ["DatabaseSettings:ConnectionString"] = "Host=localhost;Database=events_config_check;Username=events;Password=unused",
            ["Logging:LogLevel:Default"] = "Warning",
        });
        builder.Configure();
        await using var app = builder.Build();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await ConfigCheckCommand.RunAsync(app.Services, "events", ["config", "check"], output, error);

        return (exit, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[^1], error.ToString());
    }
}
