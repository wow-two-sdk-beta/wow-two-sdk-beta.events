using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Events.Tests.E2E;

[Collection(EventsApiCollection.Name)]
public sealed class SystemStatusTests(EventsApiFixture fixture)
{
    [Fact]
    public async Task Health_ShouldReturn200_WhenTheDatabaseAnswers()
    {
        using var client = fixture.Host.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Status_ShouldReturn200_WithServiceAndVersion()
    {
        using var client = fixture.Host.CreateClient();

        using var response = await client.GetAsync("/api/system/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var status = body.RootElement.GetProperty("data");
        status.GetProperty("service").GetString().Should().Be("events");
        status.GetProperty("status").GetString().Should().Be("ok");
        status.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace().And.NotBe("unknown");
        status.GetProperty("intake").GetProperty("status").GetString().Should().Be("off", "the test host configures no intake source");
    }

    /// <summary>The dev check greps the status answer for a working intake (<c>deploy.yml</c> <c>verify</c>): the pattern
    /// it uses must match what the service writes, or every rollout fails its check.</summary>
    [Fact]
    public async Task Status_ShouldMatchTheDescriptorsIntakeCheck()
    {
        using var client = fixture.Host.CreateClient();

        var body = await client.GetStringAsync("/api/system/status");

        var pattern = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(DescriptorPath()), "grep -Eq '(?<pattern>[^']+)'").Groups["pattern"].Value;
        pattern.Should().NotBeEmpty("the verify command greps the status for the intake's health");
        body.Should().MatchRegex(pattern);
    }

    private static string DescriptorPath()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, "engineering", "deployment", "deploy.yml");
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("engineering/deployment/deploy.yml was not found above the test output.");
    }
}
