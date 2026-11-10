using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>The Events module's context — a pure mapper over the SQL-owned schema in <c>Migrations/</c>, on snake_case columns.</summary>
/// <remarks>
///   - internal: consumers reach the store through <c>IGreetingsBroker</c> only, never its tables
///   - maps into the module's own schema; the template ships no table, so a domain's first table arrives as its own
///     migration, then its <c>DbSet</c> here
/// </remarks>
/// <param name="options">The context options, configured by <c>AddEvents</c>.</param>
internal sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : AppDbContextBase(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(EventsModuleConstants.SchemaName);
        base.OnModelCreating(modelBuilder);
    }
}
