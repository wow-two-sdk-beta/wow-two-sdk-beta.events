using System.Globalization;
using WoW.Two.Sdk.Beta.Be.Events.Digests;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Reads a digest's schedule as instants: each day and local time in its IANA zone is one occurrence.</summary>
/// <remarks>A local time a daylight-saving change skips runs at the first valid minute after the gap; a repeated local time
/// runs once, at its first instant. Only occurrences strictly after the schedule's start run.</remarks>
internal static class DigestOccurrences
{
    /// <summary>Holds the format of a schedule's local time.</summary>
    public const string TimeFormat = "HH:mm";

    // A schedule names at least one weekday, so a week and a day of slack cover any gap and any zone offset.
    private const int SearchDays = 8;

    /// <summary>Finds an IANA zone.</summary>
    /// <param name="id">The zone's id, such as <c>Asia/Tashkent</c>.</param>
    /// <param name="zone">The zone, when found.</param>
    /// <returns>Whether the host knows the zone.</returns>
    public static bool TryFindZone(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
            return false;

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(id, out var found))
            return false;

        zone = found;
        return true;
    }

    /// <summary>Reads a schedule's local time.</summary>
    /// <param name="text">The time, <c>HH:mm</c>.</param>
    /// <param name="time">The time, when valid.</param>
    /// <returns>Whether the text is a time.</returns>
    public static bool TryReadTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>Finds the latest occurrence at or before <paramref name="now"/> and strictly after <paramref name="since"/>.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="zone">Its zone.</param>
    /// <param name="since">The instant the schedule started from: its digest's creation or last schedule change.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The occurrence (UTC); none when no occurrence fell in between.</returns>
    public static DateTimeOffset? Latest(DigestSchedule schedule, TimeZoneInfo zone, DateTimeOffset since, DateTimeOffset now)
    {
        var today = LocalDay(now, zone);
        DateTimeOffset? latest = null;
        for (var back = -1; back <= SearchDays; back++)
        {
            foreach (var occurrence in OnDay(schedule, zone, today.AddDays(-back)))
            {
                if (occurrence <= now && occurrence > since && (latest is null || occurrence > latest))
                    latest = occurrence;
            }
        }

        return latest;
    }

    /// <summary>Finds the first occurrence strictly after <paramref name="after"/>.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="zone">Its zone.</param>
    /// <param name="after">The instant to look after.</param>
    /// <returns>The occurrence (UTC); none for a schedule without a day or a time.</returns>
    public static DateTimeOffset? Next(DigestSchedule schedule, TimeZoneInfo zone, DateTimeOffset after)
    {
        var today = LocalDay(after, zone);
        DateTimeOffset? next = null;
        for (var ahead = -1; ahead <= SearchDays; ahead++)
        {
            foreach (var occurrence in OnDay(schedule, zone, today.AddDays(ahead)))
            {
                if (occurrence > after && (next is null || occurrence < next))
                    next = occurrence;
            }
        }

        return next;
    }

    /// <summary>Gets the local day an instant falls on in a zone.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="zone">The zone.</param>
    /// <returns>The local day.</returns>
    public static DateOnly LocalDay(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    private static IEnumerable<DateTimeOffset> OnDay(DigestSchedule schedule, TimeZoneInfo zone, DateOnly day)
    {
        if (!schedule.Days.Contains(day.DayOfWeek))
            yield break;

        foreach (var text in schedule.Times)
        {
            if (TryReadTime(text, out var time))
                yield return ToUtc(day, time, zone);
        }
    }

    private static DateTimeOffset ToUtc(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        for (var step = 0; step < 24 * 60 && zone.IsInvalidTime(local); step++)
            local = local.AddMinutes(1);

        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
