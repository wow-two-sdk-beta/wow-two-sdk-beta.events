using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Review;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.CatalogEvents;

/// <summary>Maps event rows to <see cref="EventModel"/> with their sources and pending-change counts, loaded in one
/// batch per list.</summary>
internal static class EventModels
{
    private static readonly string Pending = EventsLabels.Of(ObservationState.Pending);

    /// <summary>Maps rows in their order.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="rows">The event rows.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The models, in the rows' order.</returns>
    public static async Task<List<EventModel>> LoadAsync(EventsDbContext db, string catalog, IReadOnlyCollection<CatalogEventEntity> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var ids = rows.Select(row => row.Id).ToList();
        var catalogIds = rows.Select(row => row.CatalogId).Distinct().ToList();
        var sources = (await db.Sources.AsNoTracking().Where(source => ids.Contains(source.EventId)).ToListAsync(cancellationToken))
            .ToLookup(source => source.EventId);
        var pending = await db.Observations.AsNoTracking()
            .Where(observation => observation.EventId != null && ids.Contains(observation.EventId.Value) && observation.State == Pending
                && catalogIds.Contains(observation.CatalogId))
            .GroupBy(observation => observation.EventId!.Value)
            .Select(group => new { EventId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.EventId, group => group.Count, cancellationToken);

        return rows.Select(row => EventRows.ToModel(row, catalog, sources[row.Id], pending.GetValueOrDefault(row.Id))).ToList();
    }

    /// <summary>Maps one row.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="row">The event row.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The model.</returns>
    public static async Task<EventModel> LoadAsync(EventsDbContext db, string catalog, CatalogEventEntity row, CancellationToken cancellationToken) =>
        (await LoadAsync(db, catalog, [row], cancellationToken))[0];
}
