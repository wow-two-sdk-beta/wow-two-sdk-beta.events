using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Reads PostgreSQL failures a write maps to a contract error.</summary>
internal static class StoreErrors
{
    /// <summary>Tells whether a save failed on a unique index or primary key: a concurrent writer inserted the same row.</summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
