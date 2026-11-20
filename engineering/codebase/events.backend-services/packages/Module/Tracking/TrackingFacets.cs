using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Counts what each filter chip of a tracking list would list: one grouped read per facet over the list's own
/// filtered rows, each leaving out its facet's selection, and one for the total.</summary>
/// <remarks>Nine reads in all — total, countries, cities, kinds, stages, priorities, sources by name, curated, open
/// deadline — over the catalog's published events; the catalog is small, so no index beyond the list's own.</remarks>
internal static class TrackingFacets
{
    /// <summary>Reads the facets of a query.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="caller">The caller, whose organization's tracking joins.</param>
    /// <param name="filter">The query's filters.</param>
    /// <param name="workflow">The caller organization's workflow.</param>
    /// <param name="today">The day an open deadline must still be open on (UTC).</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public static async Task<TrackingFacetsModel> ReadAsync(
        EventsDbContext db, CatalogEntity catalog, EventsCaller caller, TrackingFilter filter, WorkflowModel workflow, DateOnly today, CancellationToken cancellationToken)
    {
        var defaultStage = workflow.DefaultStage;
        IQueryable<TrackedRow> Rows(FacetOmit omit) => filter.Rows(db, catalog.Id, caller, defaultStage, today, omit);

        var total = await Rows(FacetOmit.None).CountAsync(cancellationToken);

        var countries = await Rows(FacetOmit.Countries).Where(item => item.Event.Country != null)
            .GroupBy(item => item.Event.Country!).Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var cities = await Rows(FacetOmit.Cities).Where(item => item.Event.City != null && item.Event.City != string.Empty)
            .GroupBy(item => item.Event.City!).Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var kinds = await Rows(FacetOmit.Kinds)
            .GroupBy(item => item.Event.Kind).Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var stages = await Rows(FacetOmit.Stages)
            .GroupBy(item => item.Tracking == null ? defaultStage : item.Tracking.Stage).Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var priorities = await Rows(FacetOmit.Priority).Where(item => item.Tracking != null)
            .GroupBy(item => item.Tracking!.Priority).Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var named = await (
                from item in Rows(FacetOmit.Sources)
                join source in db.Sources.AsNoTracking() on item.Event.Id equals source.EventId
                where source.Name != null
                group new { source.Name, item.Event.Id } by source.Name!.ToLower() into byName
                select new { byName.Key, Label = byName.Min(row => row.Name), Count = byName.Select(row => row.Id).Distinct().Count() })
            .ToListAsync(cancellationToken);

        var intake = EventsLabels.Of(EventSourceKind.Scfd);
        var curated = await Rows(FacetOmit.Sources)
            .CountAsync(item => !db.Sources.Any(source => source.EventId == item.Event.Id && source.Kind == intake), cancellationToken);

        var before = filter.Events.DeadlineBefore ?? today.AddDays(EventsConstants.DeadlineWindowDays);
        var withDeadline = await Rows(FacetOmit.Deadline)
            .CountAsync(item => db.Deadlines.Any(deadline => deadline.EventId == item.Event.Id && deadline.AtMin <= before && deadline.AtMax >= today), cancellationToken);

        var sources = named
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Key != EventsConstants.CuratedSource)
            .Select(item => new TrackingFacetCount(item.Key.Trim(), item.Count, item.Label?.Trim()))
            .Append(new TrackingFacetCount(EventsConstants.CuratedSource, curated));

        return new TrackingFacetsModel
        {
            Total = total,
            Countries = Ranked(countries.Select(item => new TrackingFacetCount(item.Key, item.Count))),
            Cities = Ranked(cities.Select(item => new TrackingFacetCount(item.Key, item.Count))),
            Kinds = Ordered(KindKeys(catalog), kinds.ToDictionary(item => item.Key, item => item.Count, StringComparer.Ordinal)),
            Stages = Ordered(workflow.Stages.Select(stage => stage.Key), stages.ToDictionary(item => item.Key, item => item.Count, StringComparer.Ordinal)),
            Priorities = Ordered(
                [EventsLabels.Of(TrackingPriority.High), EventsLabels.Of(TrackingPriority.Normal), EventsLabels.Of(TrackingPriority.Low)],
                priorities.ToDictionary(item => item.Key, item => item.Count, StringComparer.Ordinal)),
            Sources = Ranked(sources),
            OpenDeadline = new TrackingDeadlineFacet(before, withDeadline),
        };
    }

    /// <summary>Orders open values: most events first, then by value.</summary>
    private static IReadOnlyList<TrackingFacetCount> Ranked(IEnumerable<TrackingFacetCount> counts) =>
        counts.OrderByDescending(item => item.Count).ThenBy(item => item.Value, StringComparer.Ordinal).ToList();

    /// <summary>Lists the keys a catalog or workflow defines in its order, zeros included, then any other value found, most
    /// events first.</summary>
    private static IReadOnlyList<TrackingFacetCount> Ordered(IEnumerable<string> known, Dictionary<string, int> counts)
    {
        var keys = known.Distinct(StringComparer.Ordinal).ToList();
        var result = keys.Select(key => new TrackingFacetCount(key, counts.GetValueOrDefault(key))).ToList();
        result.AddRange(Ranked(counts.Where(pair => !keys.Contains(pair.Key, StringComparer.Ordinal)).Select(pair => new TrackingFacetCount(pair.Key, pair.Value))));
        return result;
    }

    private static IEnumerable<string> KindKeys(CatalogEntity catalog) =>
        EventsJson.Read<List<CatalogKindModel>>(catalog.Kinds).Select(kind => kind.Key);
}
