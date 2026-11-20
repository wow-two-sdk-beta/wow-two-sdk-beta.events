using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Orders the tracking list by a sort and pages it by keyset: each cursor holds the sort values of the last row
/// returned, and the next page is the rows after it, so a row that appears, goes or changes between pages never makes a
/// neighbour repeat or vanish.</summary>
/// <remarks>
///   - <c>starts</c>: start ascending with unknown last, title, id
///   - <c>added</c>: published instant descending, id descending
///   - <c>deadline</c>: the earliest start among the event's open deadlines (those whose span has not ended on the day),
///     events with none last, title, id
/// </remarks>
internal static class TrackingOrder
{
    private const string Dated = "0";
    private const string Undated = "1";

    /// <summary>Gets the wire name of a sort, as the cursor is bound to it.</summary>
    public static string Name(TrackingSort sort) => EventsLabels.Of(sort);

    /// <summary>Gets how many values the sort's cursor holds.</summary>
    public static int Arity(TrackingSort sort) => sort switch { TrackingSort.Added => 3, _ => 4 };

    /// <summary>Orders the rows by the sort and keeps those after the cursor's position.</summary>
    /// <param name="rows">The filtered rows.</param>
    /// <param name="db">The module's store, for the deadline rows.</param>
    /// <param name="sort">The sort.</param>
    /// <param name="today">The day a deadline must still be open on (UTC).</param>
    /// <param name="after">The decoded cursor; empty for the first page.</param>
    /// <returns>The ordered rows; none when the cursor does not read.</returns>
    public static IQueryable<TrackedRow>? Page(IQueryable<TrackedRow> rows, EventsDbContext db, TrackingSort sort, DateOnly today, string[] after) => sort switch
    {
        TrackingSort.Added => ByAdded(rows, after),
        TrackingSort.Deadline => ByDeadline(rows, db, today, after),
        _ => ByStarts(rows, after),
    };

    /// <summary>Encodes the position after a row.</summary>
    /// <param name="scope">The list's name.</param>
    /// <param name="sort">The sort.</param>
    /// <param name="last">The last row of the page.</param>
    public static string Encode(string scope, TrackingSort sort, TrackedRow last) => sort switch
    {
        TrackingSort.Added => KeysetCursor.Encode(
            scope, Name(sort), last.Event.PublishedAt is null ? Undated : Dated,
            last.Event.PublishedAt?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? string.Empty, last.Event.Id.ToString("D")),
        TrackingSort.Deadline => KeysetCursor.Encode(
            scope, Name(sort), last.Next is null ? Undated : Dated, Day(last.Next), last.Event.TitleSort, last.Event.Id.ToString("D")),
        _ => KeysetCursor.Encode(
            scope, Name(sort), last.Event.StartsMin is null ? Undated : Dated, Day(last.Event.StartsMin), last.Event.TitleSort, last.Event.Id.ToString("D")),
    };

    private static IQueryable<TrackedRow>? ByStarts(IQueryable<TrackedRow> rows, string[] after)
    {
        if (after.Length > 0)
        {
            if (!TryDay(after[1], after[0], out var start) || !Guid.TryParse(after[3], out var id))
                return null;
            var title = after[2];
            var dated = after[0] == Dated;
            rows = dated
                ? rows.Where(item => item.Event.StartsMin == null || item.Event.StartsMin > start
                    || (item.Event.StartsMin == start && (string.Compare(item.Event.TitleSort, title) > 0
                        || (item.Event.TitleSort == title && item.Event.Id.CompareTo(id) > 0))))
                : rows.Where(item => item.Event.StartsMin == null && (string.Compare(item.Event.TitleSort, title) > 0
                    || (item.Event.TitleSort == title && item.Event.Id.CompareTo(id) > 0)));
        }

        return rows.OrderBy(item => item.Event.StartsMin == null).ThenBy(item => item.Event.StartsMin)
            .ThenBy(item => item.Event.TitleSort).ThenBy(item => item.Event.Id);
    }

    private static IQueryable<TrackedRow>? ByAdded(IQueryable<TrackedRow> rows, string[] after)
    {
        if (after.Length > 0)
        {
            if (!Guid.TryParse(after[2], out var id) || (after[0] != Dated && after[0] != Undated) || (after[0] == Undated && after[1].Length != 0))
                return null;
            if (after[0] == Dated)
            {
                if (!long.TryParse(after[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || ticks > DateTimeOffset.MaxValue.UtcTicks)
                    return null;
                var published = new DateTimeOffset(ticks, TimeSpan.Zero);
                rows = rows.Where(item => item.Event.PublishedAt == null || item.Event.PublishedAt < published
                    || (item.Event.PublishedAt == published && item.Event.Id.CompareTo(id) < 0));
            }
            else
            {
                rows = rows.Where(item => item.Event.PublishedAt == null && item.Event.Id.CompareTo(id) < 0);
            }
        }

        return rows.OrderBy(item => item.Event.PublishedAt == null).ThenByDescending(item => item.Event.PublishedAt)
            .ThenByDescending(item => item.Event.Id);
    }

    private static IQueryable<TrackedRow>? ByDeadline(IQueryable<TrackedRow> rows, EventsDbContext db, DateOnly today, string[] after)
    {
        var ranked = rows.Select(item => new TrackedRow
        {
            Event = item.Event,
            Tracking = item.Tracking,
            Next = db.Deadlines
                .Where(deadline => deadline.EventId == item.Event.Id && deadline.AtMin != null && deadline.AtMax >= today)
                .Min(deadline => deadline.AtMin),
        });
        if (after.Length > 0)
        {
            if (!TryDay(after[1], after[0], out var next) || !Guid.TryParse(after[3], out var id))
                return null;
            var title = after[2];
            ranked = after[0] == Dated
                ? ranked.Where(item => item.Next == null || item.Next > next
                    || (item.Next == next && (string.Compare(item.Event.TitleSort, title) > 0
                        || (item.Event.TitleSort == title && item.Event.Id.CompareTo(id) > 0))))
                : ranked.Where(item => item.Next == null && (string.Compare(item.Event.TitleSort, title) > 0
                    || (item.Event.TitleSort == title && item.Event.Id.CompareTo(id) > 0)));
        }

        return ranked.OrderBy(item => item.Next == null).ThenBy(item => item.Next)
            .ThenBy(item => item.Event.TitleSort).ThenBy(item => item.Event.Id);
    }

    private static string Day(DateOnly? day) => day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Reads a cursor's day: required after a dated row, empty after an undated one.</summary>
    private static bool TryDay(string text, string flag, out DateOnly day)
    {
        day = default;
        return flag switch
        {
            Undated => text.Length == 0,
            Dated => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day),
            _ => false,
        };
    }
}
