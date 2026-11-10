using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Beta.Be.Events.Module;

namespace Events.Tests.Unit.Shared;

/// <summary>The module's clock keeps the store's precision, so a Linux clock's 100-nanosecond ticks never make a write's
/// answer differ from a later read (CI run 42775904391).</summary>
public sealed class StoredClockTests
{
    [Fact]
    public void StoredUtcNow_ShouldDropSubMicrosecondTicks_WhenTheClockHasThem()
    {
        var instant = new DateTimeOffset(2026, 11, 10, 7, 33, 41, TimeSpan.Zero).AddTicks(1_654_633);
        var clock = new FakeTimeProvider(instant);

        var stored = clock.StoredUtcNow();

        stored.Should().Be(new DateTimeOffset(2026, 11, 10, 7, 33, 41, TimeSpan.Zero).AddTicks(1_654_630));
        stored.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Truncate_ShouldKeepAnInstantAlreadyInMicroseconds()
    {
        var instant = new DateTimeOffset(2026, 11, 10, 7, 33, 41, TimeSpan.FromHours(5)).AddTicks(1_654_630);

        StoredClock.Truncate(instant).Should().Be(instant).And.Subject!.Value.Offset.Should().Be(TimeSpan.Zero);
    }
}
