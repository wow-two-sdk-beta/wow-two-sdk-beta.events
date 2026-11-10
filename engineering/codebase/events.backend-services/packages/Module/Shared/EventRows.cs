using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Maps event rows to the contract and keeps their filter columns in step with their facts.</summary>
internal static class EventRows
{
    /// <summary>Writes validated facts into a row: the jsonb and every column lists filter and sort on.</summary>
    /// <param name="row">The row.</param>
    /// <param name="facts">The validated facts.</param>
    /// <returns>The deadline rows to store for the event, replacing any before.</returns>
    public static List<EventDeadlineEntity> ApplyFacts(CatalogEventEntity row, EventFacts facts)
    {
        row.Facts = EventsJson.Write(facts);
        row.Key = facts.Key;
        row.Kind = facts.Kind;
        row.Status = EventsLabels.Of(facts.Status);
        row.Country = facts.Place?.Country;
        row.City = facts.Place?.City?.ToLowerInvariant();
        row.TitleSort = FirstTitle(facts).ToLowerInvariant();
        row.Search = string.Join(' ', facts.Title.Values
                .Append(facts.Organizer?.Name)
                .Append(facts.Place?.City)
                .Append(facts.Place?.Venue)
                .Append(facts.Key)
                .Where(text => !string.IsNullOrEmpty(text)))
            .ToLowerInvariant();

        var starts = DateSpan.Of(facts.Starts);
        var ends = DateSpan.Of(facts.Ends);
        row.StartsMin = starts?.Min;
        row.StartsMax = starts?.Max;
        row.EndsMax = ends?.Max ?? starts?.Max;

        return facts.Deadlines
            .Select((deadline, index) => (deadline, index, span: DateSpan.Of(deadline.At)))
            .Select(item => new EventDeadlineEntity
            {
                EventId = row.Id,
                Ordinal = item.index,
                Kind = EventsLabels.Of(item.deadline.Kind),
                AtMin = item.span?.Min,
                AtMax = item.span?.Max,
            })
            .ToList();
    }

    /// <summary>Gets the first title, preferring a known locale over <c>und</c>.</summary>
    public static string FirstTitle(EventFacts facts) =>
        facts.Title.OrderBy(pair => pair.Key == EventsConstants.UnknownLocale ? 1 : 0).ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value).FirstOrDefault() ?? string.Empty;

    /// <summary>Maps a row to the contract.</summary>
    /// <param name="row">The row.</param>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="sources">The event's source identities.</param>
    /// <param name="pendingChanges">How many pending observations target it.</param>
    public static EventModel ToModel(CatalogEventEntity row, string catalog, IEnumerable<EventSourceEntity> sources, int pendingChanges) => new()
    {
        Id = row.Id,
        Catalog = catalog,
        Facts = EventsJson.Read<EventFacts>(row.Facts),
        Review = EventsLabels.Parse<EventReviewState>(row.Review),
        MergedInto = row.MergedInto,
        Sources = sources
            .OrderBy(source => source.FirstSeenAt)
            .Select(source => new EventSourceModel
            {
                Kind = EventsLabels.Parse<EventSourceKind>(source.Kind),
                Id = source.SourceId,
                Url = source.Url,
                FirstSeenAtUtc = source.FirstSeenAt,
                LastSeenAtUtc = source.LastSeenAt,
            })
            .ToList(),
        PendingChanges = pendingChanges,
        Revision = row.Revision,
        CreatedAtUtc = row.CreatedAt,
        UpdatedAtUtc = row.UpdatedAt,
        PublishedAtUtc = row.PublishedAt,
        ReviewedBy = row.ReviewedBy,
        ReviewReason = row.ReviewReason,
        ReviewedAtUtc = row.ReviewedAt,
    };
}
