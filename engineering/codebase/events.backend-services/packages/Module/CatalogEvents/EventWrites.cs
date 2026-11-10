using Microsoft.EntityFrameworkCore;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.CatalogEvents;

/// <summary>Writes validated facts into an event and its deadline rows, and maps store failures to the contract's
/// errors: the one write path for curators, reviewers and intake.</summary>
internal static class EventWrites
{
    private const string KeyIndex = "ux_events_catalog_events_key";

    /// <summary>Writes facts into a tracked or new row and replaces its deadline rows in place.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="row">The event row; a new row is not yet in the store.</param>
    /// <param name="facts">The validated facts.</param>
    /// <param name="isNew">Whether the row is new, so it has no deadline rows yet.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public static async Task ApplyAsync(EventsDbContext db, CatalogEventEntity row, EventFacts facts, bool isNew, CancellationToken cancellationToken)
    {
        var wanted = EventRows.ApplyFacts(row, facts);
        var existing = isNew
            ? []
            : await db.Deadlines.Where(deadline => deadline.EventId == row.Id).OrderBy(deadline => deadline.Ordinal).ToListAsync(cancellationToken);

        // In place: a removed and re-added row with the same key would collide in the change tracker.
        for (var index = 0; index < wanted.Count; index++)
        {
            var target = existing.FirstOrDefault(deadline => deadline.Ordinal == wanted[index].Ordinal);
            if (target is null)
            {
                db.Deadlines.Add(wanted[index]);
                continue;
            }

            target.Kind = wanted[index].Kind;
            target.AtMin = wanted[index].AtMin;
            target.AtMax = wanted[index].AtMax;
        }

        db.Deadlines.RemoveRange(existing.Where(deadline => deadline.Ordinal >= wanted.Count));
    }

    /// <summary>Tells whether another event of the catalog already holds the key.</summary>
    /// <param name="db">The module's store.</param>
    /// <param name="catalogId">The catalog.</param>
    /// <param name="key">The key; none is never taken.</param>
    /// <param name="exceptId">The event being written, which may keep its own key.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    public static async Task<bool> KeyTakenAsync(EventsDbContext db, Guid catalogId, string? key, Guid exceptId, CancellationToken cancellationToken) =>
        key is not null
        && await db.Events.AnyAsync(row => row.CatalogId == catalogId && row.Key == key && row.Id != exceptId, cancellationToken);

    /// <summary>Maps a failed save to the contract's error: a stale revision, or a key another write took first.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The error; none when the failure is not one the contract names.</returns>
    public static AppError? ErrorOf(DbUpdateException exception) => exception switch
    {
        DbUpdateConcurrencyException => EventsErrors.RevisionConflict(),
        { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: KeyIndex } } => EventsErrors.KeyTaken(),
        { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => EventsErrors.RevisionConflict(),
        _ => null,
    };
}
