using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Unit.Intake;

/// <summary>The intake's settings: what a host must configure for a source.</summary>
public sealed class EventsIntakeOptionsValidatorTests
{
    [Fact]
    public void Validate_ShouldAcceptTwoSourcesIntoOneCatalog()
    {
        var result = Validate(Source("afisha-fashion"), Source("afisha-exhibitions"));

        result.Succeeded.Should().BeTrue(result.FailureMessage);
    }

    [Fact]
    public void Validate_ShouldRefuseDuplicateOrMalformedNames()
    {
        var result = Validate(Source("afisha"), Source("afisha"), Source("Not Valid"));

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("used twice")).And.Contain(failure => failure.Contains("Sources:2:Name"));
    }

    [Fact]
    public void Validate_ShouldRefuseBadPagingAndUrls()
    {
        var source = Source("bad");
        source.PageSize = 501;
        source.MaxPagesPerPoll = 0;
        source.PollInterval = TimeSpan.Zero;
        source.BaseUrl = "not a url";
        source.TokenEndpoint = "ftp://issuer.test/token";
        source.ClientId = " ";

        var failures = Validate(source).Failures!.ToList();

        failures.Should().HaveCount(6);
    }

    [Fact]
    public void Validate_ShouldNotNeedAUrl_ForADisabledSource()
    {
        var source = Source("off");
        source.Enabled = false;
        source.BaseUrl = string.Empty;

        Validate(source).Succeeded.Should().BeTrue();
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(params EventsIntakeSourceOptions[] sources)
    {
        var options = new EventsIntakeOptions();
        foreach (var source in sources)
            options.Sources.Add(source);
        return new EventsIntakeOptionsValidator().Validate(null, options);
    }

    private static EventsIntakeSourceOptions Source(string name) => new()
    {
        Name = name,
        Catalog = "ocharo-fashion",
        BaseUrl = "https://harvest.test",
        SubscriptionId = Guid.NewGuid().ToString("D"),
        TokenEndpoint = "https://identity.test/connect/token",
        ClientSecret = "test-secret",
    };
}
