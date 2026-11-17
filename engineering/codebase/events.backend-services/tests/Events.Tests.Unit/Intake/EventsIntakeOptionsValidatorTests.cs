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
    public void Validate_ShouldKeepASourceOff_WhenItsSubscriptionIsNotTypedYet()
    {
        // deploy.yml declares both Afisha sources with defaults before Wheelhouse types their subscription ids.
        var declared = new EventsIntakeSourceOptions { Name = "afisha-exhibitions", Catalog = "ocharo-fashion", BaseUrl = "https://scfd.test/" };

        var result = Validate(Source("afisha-fashion"), declared);

        result.Succeeded.Should().BeTrue(result.FailureMessage);
        IntakeSources.IsActive(declared).Should().BeFalse();
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

    [Theory]
    [InlineData("exhibition", true)]
    [InlineData("Trade-Fair", true)]
    [InlineData(null, true)]
    [InlineData("  ", true)]
    [InlineData("not a kind", false)]
    [InlineData("-exhibition", false)]
    public void Validate_ShouldTakeADefaultKind_OnlyAsAKindKey(string? defaultKind, bool valid)
    {
        var source = Source("afisha-exhibitions");
        source.DefaultKind = defaultKind;

        var result = Validate(source);

        result.Succeeded.Should().Be(valid, result.FailureMessage);
        if (!valid)
            result.Failures.Should().ContainSingle().Which.Should().Contain("Sources:0:DefaultKind");
    }

    [Theory]
    [InlineData("Asia/Tashkent", true)]
    [InlineData(" Asia/Tashkent ", true)]
    [InlineData(null, true)]
    [InlineData("  ", true)]
    [InlineData("Asia/Toshkent", false)]
    [InlineData("+05:00", false)]
    public void Validate_ShouldTakeADefaultTimeZone_OnlyAsAKnownIanaZone(string? zone, bool valid)
    {
        var source = Source("afisha-fashion");
        source.DefaultTimeZone = zone;

        var result = Validate(source);

        result.Succeeded.Should().Be(valid, result.FailureMessage);
        if (!valid)
            result.Failures.Should().ContainSingle().Which.Should().Contain("Sources:0:DefaultTimeZone");
    }

    [Fact]
    public void Validate_ShouldAcceptDefaultCities_AndRefuseABlankOrOverlongName()
    {
        var named = Source("afisha-fashion");
        named.DefaultCities.Add("Ташкент");
        named.DefaultCities.Add("Qo'qon");
        var blank = Source("afisha-exhibitions");
        blank.DefaultCities.Add("  ");
        var overlong = Source("anons-events");
        overlong.DefaultCities.Add(new string('x', 201));

        Validate(named).Succeeded.Should().BeTrue();
        var failures = Validate(named, blank, overlong).Failures!.ToList();

        failures.Should().HaveCount(2);
        failures.Should().Contain(failure => failure.Contains("Sources:1:DefaultCities")).And.Contain(failure => failure.Contains("Sources:2:DefaultCities"));
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
