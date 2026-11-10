using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Calendar;

/// <summary>Writes events as an iCalendar (RFC 5545) file: CRLF lines folded at 75 octets, text escaped.</summary>
/// <remarks>
///   - each event is one VEVENT, UID <c>{id}@events</c>, SEQUENCE its revision, DTSTAMP its last change (UTC)
///   - a day start is all-day, DTEND exclusive (the last known day + 1); a minute start is local time in its zone
///     (<c>TZID</c>, an IANA id clients resolve; no VTIMEZONE is written) or floating without one; month, year and unknown
///     starts write no entry, since a day is never invented
///   - each day or minute deadline is its own all-day VEVENT, UID <c>{id}-deadline-{ordinal}@events</c>
/// </remarks>
internal static class CalendarWriter
{
    /// <summary>Holds the PRODID the file names.</summary>
    public const string ProductId = "-//WoW2//Events//EN";

    private const int MaxLineOctets = 75;
    private const string LineBreak = "\r\n";

    /// <summary>Writes the calendar.</summary>
    /// <param name="calendarName">The calendar's name (X-WR-CALNAME), the catalog's.</param>
    /// <param name="events">The events, in order.</param>
    /// <returns>The file's text.</returns>
    public static string Write(string calendarName, IEnumerable<EventModel> events)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:" + ProductId,
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "X-WR-CALNAME:" + Text(calendarName),
        };

        foreach (var item in events)
        {
            AddEvent(lines, item);
            AddDeadlines(lines, item);
        }

        lines.Add("END:VCALENDAR");

        var builder = new StringBuilder();
        foreach (var line in lines)
            builder.Append(Fold(line)).Append(LineBreak);
        return builder.ToString();
    }

    /// <summary>Escapes a TEXT value: backslash, semicolon, comma and line breaks.</summary>
    public static string Text(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal)
        .Replace("\r\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Folds a content line so no physical line exceeds 75 octets of UTF-8, never splitting a character; each
    /// continuation line starts with one space.</summary>
    public static string Fold(string line)
    {
        if (Encoding.UTF8.GetByteCount(line) <= MaxLineOctets)
            return line;

        var builder = new StringBuilder(line.Length + (line.Length / 60 * 3));
        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            if (octets + rune.Utf8SequenceLength > MaxLineOctets)
            {
                builder.Append(LineBreak).Append(' ');
                octets = 1;
            }

            builder.Append(rune.ToString());
            octets += rune.Utf8SequenceLength;
        }

        return builder.ToString();
    }

    private static void AddEvent(List<string> lines, EventModel item)
    {
        var facts = item.Facts;
        string start;
        string? end;
        switch (facts.Starts?.Precision)
        {
            case DatePrecision.Day when Day(facts.Starts.Value) is { } day:
                var last = LastDay(facts.Ends) is { } ends && ends >= day ? ends : day;
                start = "DTSTART;VALUE=DATE:" + Date(day);
                end = "DTEND;VALUE=DATE:" + Date(last.AddDays(1));
                break;
            case DatePrecision.Minute when Minute(facts.Starts.Value) is { } local:
                var zone = string.IsNullOrWhiteSpace(facts.TimeZone) ? string.Empty : ";TZID=" + Parameter(facts.TimeZone.Trim());
                start = "DTSTART" + zone + ":" + Local(local);
                end = EndTime(facts.Ends, local) is { } until ? "DTEND" + zone + ":" + Local(until) : null;
                break;
            default:
                return;
        }

        var title = EventRows.FirstTitle(facts);
        lines.Add("BEGIN:VEVENT");
        lines.Add($"UID:{item.Id}@events");
        lines.Add("SEQUENCE:" + item.Revision.ToString(CultureInfo.InvariantCulture));
        lines.Add("DTSTAMP:" + Stamp(item.UpdatedAtUtc));
        lines.Add(start);
        if (end is not null)
            lines.Add(end);
        lines.Add("SUMMARY:" + Text(title));
        if (Location(facts.Place) is { } location)
            lines.Add("LOCATION:" + Text(location));
        if (Uri(facts.Website) is { } website)
            lines.Add("URL:" + website);
        if (facts.Status == EventStatus.Cancelled)
            lines.Add("STATUS:CANCELLED");
        lines.Add("END:VEVENT");
    }

    private static void AddDeadlines(List<string> lines, EventModel item)
    {
        var facts = item.Facts;
        var title = EventRows.FirstTitle(facts);
        var deadlines = facts.Deadlines ?? [];
        for (var ordinal = 0; ordinal < deadlines.Count; ordinal++)
        {
            var deadline = deadlines[ordinal];
            var day = deadline.At?.Precision switch
            {
                DatePrecision.Day => Day(deadline.At.Value),
                DatePrecision.Minute => Minute(deadline.At.Value) is { } local ? DateOnly.FromDateTime(local) : null,
                _ => null,
            };
            if (day is null)
                continue;

            lines.Add("BEGIN:VEVENT");
            lines.Add($"UID:{item.Id}-deadline-{ordinal.ToString(CultureInfo.InvariantCulture)}@events");
            lines.Add("SEQUENCE:" + item.Revision.ToString(CultureInfo.InvariantCulture));
            lines.Add("DTSTAMP:" + Stamp(item.UpdatedAtUtc));
            lines.Add("DTSTART;VALUE=DATE:" + Date(day.Value));
            lines.Add("DTEND;VALUE=DATE:" + Date(day.Value.AddDays(1)));
            lines.Add("SUMMARY:" + Text($"{EventsLabels.Of(deadline.Kind)}: {title}"));
            if (Uri(deadline.Url ?? facts.Website) is { } url)
                lines.Add("URL:" + url);
            if (facts.Status == EventStatus.Cancelled)
                lines.Add("STATUS:CANCELLED");
            lines.Add("END:VEVENT");
        }
    }

    // The last day an end covers, when it names one: a day, or a minute's day.
    private static DateOnly? LastDay(EventWhen? ends) => ends?.Precision switch
    {
        DatePrecision.Day => Day(ends.Value),
        DatePrecision.Minute => Minute(ends.Value) is { } local ? DateOnly.FromDateTime(local) : null,
        _ => null,
    };

    // The end of a minute start: a minute end as given, a day end as the next midnight, nothing otherwise.
    private static DateTime? EndTime(EventWhen? ends, DateTime start)
    {
        var until = ends?.Precision switch
        {
            DatePrecision.Minute => Minute(ends.Value),
            DatePrecision.Day => Day(ends.Value) is { } day ? day.AddDays(1).ToDateTime(TimeOnly.MinValue) : null,
            _ => null,
        };
        return until >= start ? until : null;
    }

    private static DateOnly? Day(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static DateTime? Minute(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ? local : null;

    private static string Date(DateOnly day) => day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string Local(DateTime local) => local.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string? Location(EventPlace? place)
    {
        if (place is null)
            return null;

        var parts = new[] { place.Venue, place.Address, place.City, place.Region, place.Country }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    // A URI value is not TEXT: it is written as is, without line breaks.
    private static string? Uri(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null : url.Trim().Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);

    // A parameter value holding a colon, semicolon or comma is quoted; a double quote cannot appear in one.
    private static string Parameter(string value)
    {
        var clean = value.Replace("\"", string.Empty, StringComparison.Ordinal);
        return clean.IndexOfAny([':', ';', ',']) >= 0 ? $"\"{clean}\"" : clean;
    }
}
