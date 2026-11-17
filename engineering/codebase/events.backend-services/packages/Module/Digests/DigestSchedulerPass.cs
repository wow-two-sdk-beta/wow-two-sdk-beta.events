using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>One turn of the digests' scheduler: starts every occurrence due by now, then sends every run whose lease is
/// free — new runs, runs a dead host left, and retries that came due. Each digest and run in its own scope; a failure is
/// logged and the turn goes on.</summary>
/// <param name="scopes">Opens a scope per digest and run.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">The log.</param>
internal sealed partial class DigestSchedulerPass(IServiceScopeFactory scopes, TimeProvider time, ILogger<DigestSchedulerPass> logger)
{
    /// <summary>Runs one turn.</summary>
    /// <param name="cancellationToken">The token that cancels the turn; the only exception it lets through.</param>
    /// <returns>How many runs it started.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.StoredUtcNow();
        List<Guid> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Digests.AsNoTracking()
                .Where(digest => digest.Enabled && digest.NextRunAt != null && digest.NextRunAt <= now)
                .OrderBy(digest => digest.NextRunAt)
                .Select(digest => digest.Id)
                .ToListAsync(cancellationToken);
        }

        var started = 0;
        foreach (var digestId in due)
        {
            if (await TryAsync<Guid?>(digestId, (runner, ct) => runner.StartDueAsync(digestId, ct), cancellationToken) is not null)
                started++;
        }

        List<Guid> runs;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var running = DigestRows.Running;
            runs = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().DigestRuns.AsNoTracking()
                .Where(run => run.Status == running && (run.LeaseUntil == null || run.LeaseUntil <= now))
                .OrderBy(run => run.StartedAt)
                .Select(run => run.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (var runId in runs)
            await TryAsync<object?>(runId, async (runner, ct) => { await runner.SendAsync(runId, ct); return null; }, cancellationToken);

        return started;
    }

    private async Task<T?> TryAsync<T>(Guid id, Func<DigestRunner, CancellationToken, Task<T?>> action, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<DigestRunner>(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogFailed(logger, id, exception);
            return default;
        }
    }

    [LoggerMessage(EventId = 4310, Level = LogLevel.Error, Message = "Events digests: {Id} failed this turn; the next turn retries it.")]
    private static partial void LogFailed(ILogger logger, Guid id, Exception exception);
}
