using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using Events.Tests.Integration.Catalogs;

namespace Events.Tests.Integration.Intake;

/// <summary>The intake's health in the host's status (the services sweep: during the <c>invalid_target</c> stretch the dev
/// check could not see a dead intake): a source whose last poll failed makes the intake failing.</summary>
public sealed class IntakeHealthTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    [Fact]
    public async Task Health_ShouldNameAFailingSource_AndOneThatHasNotPolledYet()
    {
        var working = await SourceAsync();
        var failing = await SourceAsync();
        var waiting = await SourceAsync();
        fixture.Feed.Append(working, ScfdRecords.Post(Guid.NewGuid(), 1, "Show"));
        fixture.Feed.Fail(failing, new HttpRequestException("SCFD answered 401.", null, System.Net.HttpStatusCode.Unauthorized));
        await fixture.PollAsync(working);
        await fixture.PollAsync(failing);

        await using var scope = fixture.Scope();
        var health = await scope.ServiceProvider.GetRequiredService<IEventsIntakeHealth>().ReadAsync();

        health.Status.Should().Be(IntakeHealth.Failing);
        health.Sources.Single(source => source.Name == working.Name).Status.Should().Be(IntakeHealth.Ok);
        health.Sources.Single(source => source.Name == failing.Name).Should().Match<IntakeSourceHealth>(source => source.Status == IntakeHealth.Failing && source.ConsecutiveFailures == 1);
        health.Sources.Single(source => source.Name == waiting.Name).Status.Should().Be(IntakeHealth.Waiting);
    }

    private async Task<EventsIntakeSourceOptions> SourceAsync()
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"health-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        return fixture.AddSource(slug);
    }
}
