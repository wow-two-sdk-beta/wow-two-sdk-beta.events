using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The shape of <see cref="IEventIntakeBroker"/>'s status, an operator's view; the pull itself is the module's,
/// proved in its own tiers.</summary>
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
}
