using AwesomeAssertions;
using Events.Tests.Integration.Catalogs;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Intake;

/// <summary>A catalog's intake freshness for its members (E23): the latest receipt and observed time over the catalog's own
/// sources, a count of the sources whose last poll failed, nothing of another catalog, and a 404 for anyone but the owner
/// organization.</summary>
public sealed class IntakeFreshnessTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    [Fact]
    public async Task Freshness_ShouldTakeTheLatestReceiptOfTheCatalogsSources_AndCountItsFailures_NotAnotherCatalogs()
    {
        var owner = Guid.NewGuid().ToString("N");
        var slug = await CatalogAsync(owner);
        var early = fixture.AddSource(slug);
        var late = fixture.AddSource(slug);
        var failing = fixture.AddSource(slug);
        fixture.AddSource(slug); // never polled: no cursor row, so it neither received nor failed
        var other = Guid.NewGuid().ToString("N");
        var otherSlug = await CatalogAsync(other);
        var otherFailing = fixture.AddSource(otherSlug);
        fixture.Feed.Append(early, ScfdRecords.Post(Guid.NewGuid(), 1, "Early show"));
        fixture.Feed.Append(late, ScfdRecords.Post(Guid.NewGuid(), 1, "Late show"));
        fixture.Feed.Fail(failing, new HttpRequestException("SCFD answered 401.", null, System.Net.HttpStatusCode.Unauthorized));
        fixture.Feed.Fail(otherFailing, new HttpRequestException("SCFD answered 503.", null, System.Net.HttpStatusCode.ServiceUnavailable));

        await fixture.PollAsync(early);
        fixture.Clock.Advance(TimeSpan.FromHours(1));
        await fixture.PollAsync(late);
        await fixture.PollAsync(failing);
        await fixture.PollAsync(otherFailing);

        var freshness = await ReadAsync(owner, slug);
        var statuses = await StatusesAsync(owner);
        var earlyStatus = statuses.Single(status => status.Name == early.Name);
        var lateStatus = statuses.Single(status => status.Name == late.Name);
        lateStatus.LastReceivedAtUtc.Should().BeAfter(earlyStatus.LastReceivedAtUtc!.Value);
        var ok = freshness.ValueOrThrow();
        ok.LastReceivedAtUtc.Should().Be(lateStatus.LastReceivedAtUtc);
        ok.NewestObservedAtUtc.Should().Be(new[] { earlyStatus.NewestObservedAtUtc, lateStatus.NewestObservedAtUtc }.Max());
        ok.FailingSources.Should().Be(1, "the failing source counts, the unpolled one has not failed, and another catalog's failure is not this catalog's");

        var otherFreshness = (await ReadAsync(other, otherSlug)).ValueOrThrow();
        otherFreshness.LastReceivedAtUtc.Should().BeNull();
        otherFreshness.NewestObservedAtUtc.Should().BeNull();
        otherFreshness.FailingSources.Should().Be(1);
    }

    [Fact]
    public async Task Freshness_ShouldNotCountAFailureOfASourceSwitchedOff_AndRecoveryClearsIt()
    {
        var owner = Guid.NewGuid().ToString("N");
        var slug = await CatalogAsync(owner);
        var source = fixture.AddSource(slug);
        fixture.Feed.Fail(source, new HttpRequestException("SCFD answered 503.", null, System.Net.HttpStatusCode.ServiceUnavailable));
        await fixture.PollAsync(source);
        (await ReadAsync(owner, slug)).ValueOrThrow().FailingSources.Should().Be(1);

        fixture.Feed.Fail(source, null);
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Back again"));
        await fixture.PollAsync(source);
        var recovered = (await ReadAsync(owner, slug)).ValueOrThrow();
        recovered.FailingSources.Should().Be(0);
        recovered.LastReceivedAtUtc.Should().NotBeNull();

        fixture.Feed.Fail(source, new HttpRequestException("SCFD answered 503.", null, System.Net.HttpStatusCode.ServiceUnavailable));
        await fixture.PollAsync(source);
        (await ReadAsync(owner, slug)).ValueOrThrow().FailingSources.Should().Be(1);
        source.Enabled = false;

        var switchedOff = (await ReadAsync(owner, slug)).ValueOrThrow();
        switchedOff.FailingSources.Should().Be(0);
        switchedOff.LastReceivedAtUtc.Should().Be(recovered.LastReceivedAtUtc, "a switched-off source's receipts still count");
    }

    [Fact]
    public async Task Freshness_ShouldFailNotFound_ForAnotherOrganization_AnAnonymousCaller_AndAnUnknownCatalog()
    {
        var owner = Guid.NewGuid().ToString("N");
        var slug = await CatalogAsync(owner);
        fixture.AddSource(slug);
        var publicSlug = $"fresh-{Guid.NewGuid():N}";
        fixture.Principal.ActAsMember(owner);
        await TestCatalogs.CreateAsync(fixture.Services, publicSlug, CatalogVisibility.Public);

        (await ReadAsync(Guid.NewGuid().ToString("N"), slug)).Should().BeOfType<Result<IntakeFreshnessModel>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.NotFound);
        (await ReadAsync(Guid.NewGuid().ToString("N"), publicSlug)).Should().BeOfType<Result<IntakeFreshnessModel>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.NotFound, "a public catalog's outside reader does not read its intake");
        (await ReadAsync(owner, $"missing-{Guid.NewGuid():N}")).Should().BeOfType<Result<IntakeFreshnessModel>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.NotFound);
        fixture.Principal.ActAsAnonymous();
        await using var scope = fixture.Scope();
        (await scope.ServiceProvider.GetRequiredService<IEventIntakeBroker>().GetFreshnessAsync(slug))
            .Should().BeOfType<Result<IntakeFreshnessModel>.Failure>().Which.Error.Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public void Freshness_ShouldCarryTimesAndACountOnly()
    {
        typeof(IntakeFreshnessModel).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(nameof(IntakeFreshnessModel.LastReceivedAtUtc), nameof(IntakeFreshnessModel.NewestObservedAtUtc), nameof(IntakeFreshnessModel.FailingSources));
    }

    private async Task<string> CatalogAsync(string organization)
    {
        fixture.Principal.ActAsMember(organization);
        var slug = $"fresh-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        return slug;
    }

    private async Task<Result<IntakeFreshnessModel>> ReadAsync(string organization, string slug)
    {
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        return await scope.ServiceProvider.GetRequiredService<IEventIntakeBroker>().GetFreshnessAsync(slug);
    }

    private async Task<IReadOnlyList<IntakeSourceStatusModel>> StatusesAsync(string organization)
    {
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        return (await scope.ServiceProvider.GetRequiredService<IEventIntakeBroker>().GetStatusAsync()).ValueOrThrow().Sources;
    }
}
