using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Finds the event a new post's source already reported, so one fair on two sources stays one event
/// (events-integration E17).</summary>
/// <remarks>
///   - a match is a candidate or published event of the same catalog, no source's hold, whose title matches
///     (<see cref="EventTitleKeys"/>), whose dates overlap the post's and whose city is equal, or both none
///   - an undated post matches nothing, and neither does an undated event: the rule never guesses a date
///   - one query reads the events of the catalog that the dates, the city and the keep-apart pairs leave; the titles are
///     compared in memory, never one query per event
///   - several matches: the closest title, then the nearest start, then a published event, then the oldest
///   - a source a reviewer split off an event (<c>events_event_keep_apart</c>) never matches that event again
/// </remarks>
internal static class CrossSourceMatcher
{
    /// <summary>Holds the rule's name, recorded as the attached source's <c>MatchedBy</c>.</summary>
    public const string Rule = "rule:title-date-city";

    private static readonly string Candidate = EventsLabels.Of(EventReviewState.Candidate);
    private static readonly string Published = EventsLabels.Of(EventReviewState.Published);

    /// <summary>Finds the event to attach a new source identity to.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="catalogId">The catalog.</param>
    /// <param name="sourceKind">The new source's kind, as stored.</param>
    /// <param name="sourceId">The new source's identity.</param>
    /// <param name="facts">The facts the post proposes.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The matching event, tracked; none when nothing matches.</returns>
    public static async Task<CatalogEventEntity?> FindAsync(
        EventsDbContext db, Guid catalogId, string sourceKind, string sourceId, EventFacts facts, CancellationToken cancellationToken)
    {
        var words = facts.Title.Values.Select(EventTitleKeys.Words).Where(set => set.Count > 0).ToList();
        if (words.Count == 0 || DateSpan.Of(facts.Starts) is not { } starts)
            return null;

        var first = starts.Min;
        var last = DateSpan.Of(facts.Ends)?.Max ?? starts.Max;
        var city = string.IsNullOrWhiteSpace(facts.Place?.City) ? null : facts.Place!.City!.Trim();
        var sourceMark = IntakeProcessor.SourceActor;

        var rows = db.Events.AsNoTracking()
            .Where(row => row.CatalogId == catalogId && (row.Review == Candidate || row.Review == Published)
                && row.StartsMin != null && row.StartsMin <= last && row.EndsMax >= first
                && (row.ReviewedBy == null || !row.ReviewedBy.StartsWith(sourceMark))
                && !db.KeepApart.Any(apart => apart.EventId == row.Id && apart.CatalogId == catalogId
                    && apart.SourceKind == sourceKind && apart.SourceId == sourceId));
        if (city is null)
        {
            rows = rows.Where(row => row.City == null);
        }
        else
        {
            var cities = new[] { city.ToLowerInvariant(), CityNames.Canonical(city).ToLowerInvariant() }.Distinct().ToList();
            rows = rows.Where(row => row.City != null && cities.Contains(row.City));
        }

        var found = await rows
            .Select(row => new { row.Id, row.Facts, row.StartsMin, row.Review, row.CreatedAt })
            .ToListAsync(cancellationToken);
        var best = found
            .Select(row => (Row: row, Score: Score(words, row.Facts)))
            .Where(item => item.Score >= EventTitleKeys.MinOverlap)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => Math.Abs(item.Row.StartsMin!.Value.DayNumber - first.DayNumber))
            .ThenBy(item => item.Row.Review == Published ? 0 : 1)
            .ThenBy(item => item.Row.CreatedAt)
            .ThenBy(item => item.Row.Id)
            .Select(item => item.Row.Id)
            .FirstOrDefault();

        return best == Guid.Empty ? null : await db.Events.FirstOrDefaultAsync(row => row.Id == best, cancellationToken);
    }

    private static double Score(List<HashSet<string>> incoming, string facts)
    {
        var best = 0d;
        using var document = JsonDocument.Parse(facts);
        if (!document.RootElement.TryGetProperty("title", out var titles) || titles.ValueKind != JsonValueKind.Object)
            return best;

        foreach (var title in titles.EnumerateObject())
        {
            if (title.Value.ValueKind != JsonValueKind.String)
                continue;

            var words = EventTitleKeys.Words(title.Value.GetString());
            foreach (var one in incoming)
                best = Math.Max(best, EventTitleKeys.Overlap(one, words));
        }

        return best;
    }
}
