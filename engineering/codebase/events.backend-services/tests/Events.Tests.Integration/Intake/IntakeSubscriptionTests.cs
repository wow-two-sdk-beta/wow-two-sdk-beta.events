using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Events.Tests.Integration.Catalogs;

namespace Events.Tests.Integration.Intake;

/// <summary>Intake checks the SCFD subscription before every pull (the services sweep's safety gap): under <c>auto</c>
/// review with the <c>filter</c> classifier everything delivered publishes, so a subscription without the source's
/// filter, on another schema or suspended pulls nothing.</summary>
public sealed class IntakeSubscriptionTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    private const string Filter = "ocharo-fashion-v1";

    [Theory]
    [InlineData(null, "wow2.event.v1", "active", "filter-mismatch")]
    [InlineData("another-filter", "wow2.event.v1", "active", "filter-mismatch")]
    [InlineData(Filter, "wow2.post.v1", "active", "schema-mismatch")]
    [InlineData(Filter, "wow2.event.v1", "suspended", "state-suspended")]
    public async Task Intake_ShouldPullNothing_WhenTheSubscriptionDoesNotMatchTheSource(string? filterKey, string schema, string state, string mismatch)
    {
        var (slug, source) = await SourceAsync();
        fixture.Feed.Subscription(source, new ScfdSubscription(filterKey, schema, state));
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Delivered by an unfiltered subscription"));

        await fixture.PollAsync(source);

        fixture.Feed.Reads.Should().NotContain(read => read.Source == source.Name, "nothing is pulled");
        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty();
        (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Should().BeEmpty();
        await using var scope = fixture.Scope();
        var cursor = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().IntakeCursors.SingleAsync(row => row.SourceName == source.Name);
        cursor.LastError.Should().Contain(mismatch).And.Contain("pulls nothing");
        cursor.ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public async Task Intake_ShouldPublish_WhenTheSubscriptionCarriesTheSourcesFilter()
    {
        var (slug, source) = await SourceAsync();
        fixture.Feed.Subscription(source, new ScfdSubscription(Filter, EventsConstants.EventSchema, ScfdSubscription.ActiveState));
        fixture.Feed.Append(source, ScfdRecords.Post(Guid.NewGuid(), 1, "Show"));

        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().ContainSingle().Which.ReviewedBy.Should().Be($"classifier:filter:{Filter}");
    }

    private async Task<(string Slug, EventsIntakeSourceOptions Source)> SourceAsync()
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"subscription-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        return (slug, fixture.AddSource(slug, auto =>
        {
            auto.Review = EventsIntakeSourceOptions.AutoReview;
            auto.Filter = Filter;
        }));
    }

    private async Task<IReadOnlyList<EventModel>> EventsAsync(string slug, EventQuery query)
    {
        await using var scope = fixture.Scope();
        return (await scope.ServiceProvider.GetRequiredService<ICatalogEventsBroker>().ListAsync(slug, query)).ValueOrThrow().Items;
    }
}
