using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

namespace Events.Tests.Unit.Digests;

/// <summary>Covers a schedule read as instants: a local time in its IANA zone, the latest owed occurrence and the next one,
/// and the daylight-saving gap and repeat.</summary>
public sealed class DigestOccurrencesTests
{
    private static readonly DigestSchedule MondayTashkent = new() { Days = [DayOfWeek.Monday], Times = ["09:00"], TimeZone = "Asia/Tashkent" };
    private static readonly DateTimeOffset MondayAt0400Utc = new(2026, 11, 23, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Next_ShouldBeMonday0400Utc_ForMonday0900InTashkent()
    {
        Zone("Asia/Tashkent", out var zone);

        DigestOccurrences.Next(MondayTashkent, zone, new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)).Should().Be(MondayAt0400Utc);
        DigestOccurrences.Next(MondayTashkent, zone, MondayAt0400Utc).Should().Be(MondayAt0400Utc.AddDays(7), "the next occurrence is strictly after");
    }

    [Fact]
    public void Latest_ShouldOweTheOccurrence_FromItsInstantOn_AndOnlyAfterTheScheduleStarted()
    {
        Zone("Asia/Tashkent", out var zone);
        var since = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

        DigestOccurrences.Latest(MondayTashkent, zone, since, MondayAt0400Utc.AddSeconds(-1)).Should().BeNull();
        DigestOccurrences.Latest(MondayTashkent, zone, since, MondayAt0400Utc).Should().Be(MondayAt0400Utc);
        DigestOccurrences.Latest(MondayTashkent, zone, since, MondayAt0400Utc.AddDays(3)).Should().Be(MondayAt0400Utc);
        DigestOccurrences.Latest(MondayTashkent, zone, MondayAt0400Utc, MondayAt0400Utc.AddDays(3)).Should().BeNull("an occurrence at the schedule's start never runs");
    }

    [Fact]
    public void Latest_ShouldPickTheLatestTimeOfTheLatestDay_AcrossSeveralDaysAndTimes()
    {
        Zone("Asia/Tashkent", out var zone);
        var schedule = new DigestSchedule { Days = [DayOfWeek.Monday, DayOfWeek.Thursday], Times = ["09:00", "18:30"], TimeZone = "Asia/Tashkent" };

        DigestOccurrences.Latest(schedule, zone, DateTimeOffset.MinValue, new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2026, 11, 23, 13, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Next_ShouldRunAtTheFirstMinuteAfterTheGap_WhenDaylightSavingSkipsTheTime()
    {
        Zone("Europe/Berlin", out var zone);
        var schedule = new DigestSchedule { Days = [DayOfWeek.Sunday], Times = ["02:30"], TimeZone = "Europe/Berlin" };

        DigestOccurrences.Next(schedule, zone, new DateTimeOffset(2027, 3, 27, 12, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2027, 3, 28, 1, 0, 0, TimeSpan.Zero), "02:30 does not exist; 03:00 CEST is 01:00 UTC");
    }

    [Fact]
    public void Next_ShouldRunOnceAtTheFirstInstant_WhenDaylightSavingRepeatsTheTime()
    {
        Zone("Europe/Berlin", out var zone);
        var schedule = new DigestSchedule { Days = [DayOfWeek.Sunday], Times = ["02:30"], TimeZone = "Europe/Berlin" };
        var first = new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero);

        DigestOccurrences.Next(schedule, zone, new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero)).Should().Be(first, "02:30 CEST comes first");
        DigestOccurrences.Next(schedule, zone, first).Should().Be(first.AddDays(7).AddHours(1), "the repeat at 02:30 CET does not run again");
    }

    [Fact]
    public void TryFindZone_ShouldRefuseAnUnknownZone()
    {
        DigestOccurrences.TryFindZone("Mars/Olympus", out _).Should().BeFalse();
        DigestOccurrences.TryFindZone(null, out _).Should().BeFalse();
    }

    private static void Zone(string id, out TimeZoneInfo zone) => DigestOccurrences.TryFindZone(id, out zone).Should().BeTrue();
}
