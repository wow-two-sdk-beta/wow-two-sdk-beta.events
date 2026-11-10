using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Polls one source: page after page while more wait, up to its page limit, each page in its own scope and
/// transaction. A failed page is recorded on the source's cursor row in its own write and never thrown.</summary>
/// <param name="scopes">Opens a scope per page.</param>
/// <param name="time">The clock failures are stamped with.</param>
/// <param name="logger">The log.</param>
internal sealed partial class IntakeSourcePoller(IServiceScopeFactory scopes, TimeProvider time, ILogger<IntakeSourcePoller> logger)
{
    private const int MaxErrorLength = 2_000;
    private const string FailureSql =
        "INSERT INTO " + EventsModuleConstants.SchemaName + ".events_intake_cursors AS c " +
        "(source_name, subscription_id, last_polled_at, last_error, consecutive_failures, received, quarantined, updated_at) " +
        "VALUES ({0}, {1}, {2}, {3}, 1, 0, 0, {2}) " +
        "ON CONFLICT (source_name) DO UPDATE SET last_polled_at = EXCLUDED.last_polled_at, last_error = EXCLUDED.last_error, " +
        "consecutive_failures = c.consecutive_failures + 1, updated_at = EXCLUDED.updated_at";

    /// <summary>Polls the source now.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The token that cancels the poll; the only exception it lets through.</param>
    public async Task PollAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken)
    {
        for (var page = 0; page < Math.Max(1, source.MaxPagesPerPoll); page++)
        {
            Result<IntakePageOutcome> outcome;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                outcome = await scope.ServiceProvider.GetRequiredService<IntakeProcessor>().ProcessPageAsync(source, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogPageFailed(logger, source.Name, exception);
                await RecordFailureAsync(source, $"{exception.GetType().Name}: {exception.Message}", cancellationToken);
                return;
            }

            if (outcome is Result<IntakePageOutcome>.Failure { Error: var error })
            {
                LogPageRefused(logger, source.Name, error.Message);
                await RecordFailureAsync(source, error.Message, cancellationToken);
                return;
            }

            if (outcome is not Result<IntakePageOutcome>.Success { Value.HasMore: true })
                return;
        }
    }

    private async Task RecordFailureAsync(EventsIntakeSourceOptions source, string error, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var message = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
            await db.Database.ExecuteSqlRawAsync(FailureSql, [source.Name, IntakeSources.Subscription(source), time.GetUtcNow(), message], cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogFailureNotRecorded(logger, source.Name, exception);
        }
    }

    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "Events intake: a page of source {Source} failed; it replays on the next poll.")]
    private static partial void LogPageFailed(ILogger logger, string source, Exception exception);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Warning, Message = "Events intake: source {Source} cannot poll: {Error}")]
    private static partial void LogPageRefused(ILogger logger, string source, string error);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Error, Message = "Events intake: the failure of source {Source} could not be recorded.")]
    private static partial void LogFailureNotRecorded(ILogger logger, string source, Exception exception);
}
