using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Reads each active source's cursor row: a source failing when its last poll failed, waiting before its first.</summary>
/// <param name="db">The module's database.</param>
/// <param name="options">The intake's settings.</param>
internal sealed class IntakeHealthReader(EventsDbContext db, IOptionsMonitor<EventsIntakeOptions> options) : IEventsIntakeHealth
{
    /// <inheritdoc />
    public async Task<IntakeHealth> ReadAsync(CancellationToken cancellationToken = default)
    {
        var active = options.CurrentValue.Sources.Where(source => source is not null && IntakeSources.IsActive(source)).Select(source => source.Name).ToList();
        if (active.Count == 0)
            return new IntakeHealth(IntakeHealth.Off, []);

        var cursors = await db.IntakeCursors.AsNoTracking().Where(row => active.Contains(row.SourceName)).ToDictionaryAsync(row => row.SourceName, cancellationToken);
        var sources = active.Select(name => cursors.TryGetValue(name, out var row)
                ? new IntakeSourceHealth(name, row.ConsecutiveFailures > 0 ? IntakeHealth.Failing : IntakeHealth.Ok, row.ConsecutiveFailures, row.LastPolledAt)
                : new IntakeSourceHealth(name, IntakeHealth.Waiting, 0, null))
            .ToList();
        return new IntakeHealth(sources.Any(source => source.Status == IntakeHealth.Failing) ? IntakeHealth.Failing : IntakeHealth.Ok, sources);
    }
}
