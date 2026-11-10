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
        if (!await SubscriptionMatchesAsync(source, cancellationToken))
            return;

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
                LogPageFailed(logger, source.Name, FailureKind(exception), exception);
                await RecordFailureAsync(source, $"{exception.GetType().Name}: {exception.Message}", cancellationToken);
                return;
            }

            if (outcome is Result<IntakePageOutcome>.Failure { Error: var error })
            {
                LogPageRefused(logger, source.Name, error.Message);
                await RecordFailureAsync(source, error.Message, cancellationToken);
                return;
            }

            if (outcome is Result<IntakePageOutcome>.Success { Value: var done })
                LogPagePolled(logger, source.Name, page + 1, done.Received, done.Quarantined, done.HasMore);

            if (outcome is not Result<IntakePageOutcome>.Success { Value.HasMore: true })
                break;
        }

        await ReviewBacklogAsync(source, cancellationToken);
    }

    /// <summary>Checks the source's subscription before any pull: its filter, schema and state must be what the source
    /// expects, or nothing is pulled and the mismatch is recorded on the source's cursor row (sweep #3).</summary>
    private async Task<bool> SubscriptionMatchesAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken)
    {
        ScfdSubscription subscription;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            subscription = await scope.ServiceProvider.GetRequiredService<IObservationFeed>().ReadSubscriptionAsync(source, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogPageFailed(logger, source.Name, FailureKind(exception), exception);
            await RecordFailureAsync(source, $"{exception.GetType().Name}: {exception.Message}", cancellationToken);
            return false;
        }

        if (subscription.MismatchWith(source) is not { } mismatch)
            return true;

        var message = $"The subscription does not match the source ({mismatch}): {subscription.Describe(source)}; intake pulls nothing.";
        LogSubscriptionRefused(logger, source.Name, mismatch, subscription.Describe(source));
        await RecordFailureAsync(source, message, cancellationToken);
        return false;
    }

    /// <summary>Reviews the source's held backlog after its pages, so candidates stored before automatic review publish.</summary>
    private async Task ReviewBacklogAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken)
    {
        if (!source.ReviewsAutomatically)
            return;

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var pass = await scope.ServiceProvider.GetRequiredService<IntakeProcessor>().ReviewBacklogAsync(source, cancellationToken);
            LogBacklogPass(logger, source.Name, pass.Checked, pass.Remapped, pass.Published);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogPageFailed(logger, source.Name, FailureKind(exception), exception);
        }
    }

    private async Task RecordFailureAsync(EventsIntakeSourceOptions source, string error, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var message = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
            await db.Database.ExecuteSqlRawAsync(FailureSql, [source.Name, IntakeSources.Subscription(source), time.StoredUtcNow(), message], cancellationToken);
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

    /// <summary>Names a failure's kind for the log: SCFD's HTTP status, the token endpoint's OAuth error, or the exception
    /// type; never a token, a body or a host.</summary>
    internal static string FailureKind(Exception exception) => exception switch
    {
        WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2.OAuth2TokenRequestException token => $"token-refused:{token.Error ?? "unknown"}",
        HttpRequestException { StatusCode: { } status } => $"scfd-status-{(int)status}",
        HttpRequestException => "scfd-unreachable",
        TaskCanceledException => "timeout",
        _ => exception.GetType().Name,
    };

    [LoggerMessage(EventId = 4106, Level = LogLevel.Information, Message = "Events intake: source {Source} backlog pass: {Checked} held event(s) checked, {Remapped} re-mapped, {Published} published by its automatic review.")]
    private static partial void LogBacklogPass(ILogger logger, string source, int @checked, int remapped, int published);

    [LoggerMessage(EventId = 4107, Level = LogLevel.Warning, Message = "Events intake: source {Source} pulls nothing: its SCFD subscription does not match ({Mismatch}): {Detail}.")]
    private static partial void LogSubscriptionRefused(ILogger logger, string source, string mismatch, string detail);

    [LoggerMessage(EventId = 4104, Level = LogLevel.Information, Message = "Events intake: source {Source} page {Page} polled: SCFD 200, {Received} new records stored, {Quarantined} quarantined, more pages {HasMore}; the cursor committed.")]
    private static partial void LogPagePolled(ILogger logger, string source, int page, int received, int quarantined, bool hasMore);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "Events intake: a page of source {Source} failed ({Kind}); the cursor stays and the page replays on the next poll.")]
    private static partial void LogPageFailed(ILogger logger, string source, string kind, Exception exception);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Warning, Message = "Events intake: source {Source} cannot poll: {Error}")]
    private static partial void LogPageRefused(ILogger logger, string source, string error);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Error, Message = "Events intake: the failure of source {Source} could not be recorded.")]
    private static partial void LogFailureNotRecorded(ILogger logger, string source, Exception exception);
}
