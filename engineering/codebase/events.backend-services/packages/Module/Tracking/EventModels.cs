using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Review;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Maps a page of event rows to the contract with their sources and, for the owner, their pending changes.</summary>
internal static class EventModels
{
    /// <summary>Maps rows to events, keyed by id.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="rows">The event rows.</param>
    /// <param name="owner">Whether the caller owns the catalog; pending changes are review data, shown to the owner only.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public static async Task<Dictionary<Guid, EventModel>> LoadAsync(
        EventsDbContext db, string catalog, IReadOnlyCollection<CatalogEventEntity> rows, bool owner, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var ids = rows.Select(row => row.Id).ToArray();
        var sources = (await db.Sources.AsNoTracking().Where(source => ids.Contains(source.EventId)).ToListAsync(cancellationToken))
            .ToLookup(source => source.EventId);

        var pending = new Dictionary<Guid, int>();
        if (owner)
        {
            var state = EventsLabels.Of(ObservationState.Pending);
            pending = await db.Observations.AsNoTracking()
                .Where(observation => observation.EventId != null && ids.Contains(observation.EventId.Value) && observation.State == state)
                .GroupBy(observation => observation.EventId!.Value)
                .Select(group => new { EventId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.EventId, item => item.Count, cancellationToken);
        }

        return rows.ToDictionary(row => row.Id, row => EventRows.ToModel(row, catalog, sources[row.Id], pending.GetValueOrDefault(row.Id)));
    }
}
