using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The shape of <see cref="IEventIntakeBroker"/>'s status, an operator's view, and who may read a catalog's
/// freshness; the pull itself, and the freshness's figures over configured sources, are the module's, proved in its own
/// tiers.</summary>
public abstract class EventIntakeBrokerConformance : EventsConformance
{
    /// <summary>The status lists the sources and counts the review backlog.</summary>
    [Fact]
    public async Task GetStatusAsync_ShouldDescribeTheSources_AndTheBacklog()
    {
        var status = Value(await As(NewMember()).Intake.GetStatusAsync());

        status.Sources.Should().NotBeNull();
        status.Sources.Should().OnlyContain(source => !string.IsNullOrWhiteSpace(source.Name) && !string.IsNullOrWhiteSpace(source.Catalog));
        status.Candidates.Should().BeGreaterThanOrEqualTo(0);
        status.PendingChanges.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>A catalog with no intake source reads as never received and nothing failing, to its owner and to another
    /// member of the owner organization.</summary>
    [Fact]
    public async Task GetFreshnessAsync_ShouldReadNeverReceived_ForAMemberOfTheOwnerOrganization_OfACatalogWithNoSource()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);

        var byOwner = Value(await As(owner).Intake.GetFreshnessAsync(catalog.Slug));
        var byMember = Value(await As(NewMember(owner.OrganizationId)).Intake.GetFreshnessAsync(catalog.Slug));

        foreach (var freshness in new[] { byOwner, byMember })
        {
            freshness.LastReceivedAtUtc.Should().BeNull();
            freshness.NewestObservedAtUtc.Should().BeNull();
            freshness.FailingSources.Should().Be(0);
        }
    }

    /// <summary>Another organization reads 404, as it does for the catalog itself: a private catalog, and a public one it
    /// may read but does not own.</summary>
    [Theory]
    [InlineData(CatalogVisibility.Private)]
    [InlineData(CatalogVisibility.Public)]
    public async Task GetFreshnessAsync_ShouldFailNotFound_ForAnotherOrganization(CatalogVisibility visibility)
    {
        var catalog = await CreateCatalogAsync(NewMember(), visibility);

        Error(await As(NewMember()).Intake.GetFreshnessAsync(catalog.Slug)).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>An unknown slug is not found.</summary>
    [Fact]
    public async Task GetFreshnessAsync_ShouldFailNotFound_WhenTheSlugIsUnknown()
    {
        Error(await As(NewMember()).Intake.GetFreshnessAsync(NewSlug())).Type.Should().Be(AppErrorType.NotFound);
    }
}
