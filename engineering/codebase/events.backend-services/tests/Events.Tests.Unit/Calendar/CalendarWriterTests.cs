using System.Text;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Calendar;

namespace Events.Tests.Unit.Calendar;

/// <summary>Covers the iCalendar writer: escaping, folding, line endings and which entries each event writes.</summary>
public sealed class CalendarWriterTests
{
    private static readonly Guid EventId = Guid.Parse("0193a1b2-0000-7000-8000-000000000001");

    [Fact]
    public void Text_ShouldEscapeBackslashSemicolonCommaAndLineBreaks()
    {
        CalendarWriter.Text("a\\b;c,d\r\ne\nf\rg").Should().Be("a\\\\b\\;c\\,d\\ne\\nf\\ng");
    }

    [Fact]
    public void Fold_ShouldKeepEveryLineWithin75Octets_WithoutSplittingACharacter()
    {
        var line = "SUMMARY:" + string.Concat(Enumerable.Repeat("Неделя моды в Ташкенте — ", 6));

        var folded = CalendarWriter.Fold(line);

        var physical = folded.Split("\r\n");
        physical.Should().HaveCountGreaterThan(1);
        physical.Should().OnlyContain(part => Encoding.UTF8.GetByteCount(part) <= 75);
        physical.Skip(1).Should().OnlyContain(part => part.StartsWith(' '));
        string.Concat(physical.Select((part, index) => index == 0 ? part : part[1..])).Should().Be(line);
        CalendarWriter.Fold("SUMMARY:short").Should().Be("SUMMARY:short");
    }

    [Fact]
    public void Write_ShouldUseCrlfLinesAndTheCalendarHeader()
    {
        var content = CalendarWriter.Write("Ocharo; fashion", []);

        content.Should().Be(
            "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//WoW2//Events//EN\r\nCALSCALE:GREGORIAN\r\nMETHOD:PUBLISH\r\n" +
            "X-WR-CALNAME:Ocharo\\; fashion\r\nEND:VCALENDAR\r\n");
    }

    [Fact]
    public void Write_ShouldEndAnAllDayEventTheDayAfterItsLastDay()
    {
        var spanning = Event(Facts(Day("2026-03-14")) with { Ends = Day("2026-03-16") });
        var single = Event(Facts(Day("2026-03-14")));

        var lines = Lines(CalendarWriter.Write("Catalog", [spanning]));
        Lines(CalendarWriter.Write("Catalog", [single])).Should().Contain("DTEND;VALUE=DATE:20260315");

        lines.Should().ContainInConsecutiveOrder(
            "BEGIN:VEVENT", $"UID:{EventId}@events", "SEQUENCE:3", "DTSTAMP:20261110T090000Z",
            "DTSTART;VALUE=DATE:20260314", "DTEND;VALUE=DATE:20260317", "SUMMARY:Tashkent Fashion Week\\, spring");
        lines.Should().Contain("LOCATION:Expo Centre\\, Tashkent\\, UZ").And.Contain("URL:https://tfw.example/");
    }

    [Fact]
    public void Write_ShouldWriteLocalTimeInItsZone_WhenTheStartHasMinutePrecision()
    {
        var zoned = Event(Facts(Minute("2026-03-14T19:00")) with { Ends = Minute("2026-03-14T22:30"), TimeZone = "Asia/Tashkent" });
        var floating = Event(Facts(Minute("2026-03-14T19:00")));

        Lines(CalendarWriter.Write("Catalog", [zoned])).Should()
            .Contain("DTSTART;TZID=Asia/Tashkent:20260314T190000").And.Contain("DTEND;TZID=Asia/Tashkent:20260314T223000");
        var lines = Lines(CalendarWriter.Write("Catalog", [floating]));
        lines.Should().Contain("DTSTART:20260314T190000");
        lines.Should().NotContain(line => line.StartsWith("DTEND"));
    }

    [Fact]
    public void Write_ShouldOmitTheEvent_WhenItsStartHasMonthOrYearPrecision()
    {
        var month = Event(Facts(new EventWhen { Value = "2026-10", Precision = DatePrecision.Month, Certainty = DateCertainty.Estimated }));
        var year = Event(Facts(new EventWhen { Value = "2027", Precision = DatePrecision.Year }));
        var unknown = Event(Facts(new EventWhen()));

        CalendarWriter.Write("Catalog", [month, year, unknown]).Should().NotContain("BEGIN:VEVENT");
    }

    [Fact]
    public void Write_ShouldWriteEachExactDeadlineAsItsOwnAllDayEntry()
    {
        var facts = Facts(new EventWhen { Value = "2026-10", Precision = DatePrecision.Month }) with
        {
            Status = EventStatus.Cancelled,
            Deadlines =
            [
                new EventDeadline { Kind = DeadlineKind.Application, At = Day("2026-08-15") },
                new EventDeadline { Kind = DeadlineKind.Payment, At = new EventWhen { Value = "2026-09", Precision = DatePrecision.Month } },
                new EventDeadline { Kind = DeadlineKind.EarlyBird, At = Minute("2026-09-01T18:00"), Url = "https://tfw.example/tickets" },
            ],
        };

        var lines = Lines(CalendarWriter.Write("Catalog", [Event(facts)]));

        lines.Count(line => line == "BEGIN:VEVENT").Should().Be(2, "the month start and the month deadline write no entry");
        lines.Should().ContainInConsecutiveOrder(
            "BEGIN:VEVENT", $"UID:{EventId}-deadline-0@events", "SEQUENCE:3", "DTSTAMP:20261110T090000Z",
            "DTSTART;VALUE=DATE:20260815", "DTEND;VALUE=DATE:20260816", "SUMMARY:application: Tashkent Fashion Week\\, spring",
            "URL:https://tfw.example/", "STATUS:CANCELLED", "END:VEVENT");
        lines.Should().ContainInConsecutiveOrder(
            $"UID:{EventId}-deadline-2@events", "SEQUENCE:3", "DTSTAMP:20261110T090000Z",
            "DTSTART;VALUE=DATE:20260901", "DTEND;VALUE=DATE:20260902", "SUMMARY:earlyBird: Tashkent Fashion Week\\, spring",
            "URL:https://tfw.example/tickets");
    }

    private static List<string> Lines(string content) => content.Replace("\r\n ", string.Empty).Split("\r\n").ToList();

    private static EventWhen Day(string value) => new() { Value = value, Precision = DatePrecision.Day };

    private static EventWhen Minute(string value) => new() { Value = value, Precision = DatePrecision.Minute };

    private static EventFacts Facts(EventWhen starts) => new()
    {
        Title = new Dictionary<string, string> { ["en"] = "Tashkent Fashion Week, spring" },
        Place = new EventPlace { Venue = "Expo Centre", City = "Tashkent", Country = "UZ" },
        Website = "https://tfw.example/",
        Starts = starts,
    };

    private static EventModel Event(EventFacts facts) => new()
    {
        Id = EventId,
        Catalog = "ocharo-fashion",
        Facts = facts,
        Review = EventReviewState.Published,
        Revision = 3,
        CreatedAtUtc = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero),
        UpdatedAtUtc = new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.FromHours(5)),
    };
}
