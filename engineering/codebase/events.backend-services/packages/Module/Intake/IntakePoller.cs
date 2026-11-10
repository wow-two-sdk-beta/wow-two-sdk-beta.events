using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Polls every enabled source on its interval, one source after another in a single loop, so two sources never
/// race on one post; never crashes the host.</summary>
/// <param name="options">The intake's settings.</param>
/// <param name="poller">Polls one source.</param>
/// <param name="time">The clock the intervals run on.</param>
/// <param name="logger">The log.</param>
internal sealed partial class IntakePoller(
    IOptionsMonitor<EventsIntakeOptions> options,
    IntakeSourcePoller poller,
    TimeProvider time,
    ILogger<IntakePoller> logger) : BackgroundService
{
    private static readonly TimeSpan MaxIdle = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        var active = options.CurrentValue.Sources.Where(IntakeSources.IsActive).Select(source => source.Name).ToList();
        LogStarted(logger, active.Count, string.Join(", ", active));
        var due = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = MaxIdle;
            try
            {
                foreach (var source in options.CurrentValue.Sources.Where(IntakeSources.IsActive))
                {
                    var now = time.StoredUtcNow();
                    if (due.TryGetValue(source.Name, out var at) && at > now)
                    {
                        wait = Min(wait, at - now);
                        continue;
                    }

                    await poller.PollAsync(source, stoppingToken);
                    due[source.Name] = time.StoredUtcNow() + source.PollInterval;
                    wait = Min(wait, source.PollInterval);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogLoopFailed(logger, exception);
            }

            try
            {
                await Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(1), time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;

    [LoggerMessage(EventId = 4105, Level = LogLevel.Information, Message = "Events intake: polling {Count} source(s): {Sources}.")]
    private static partial void LogStarted(ILogger logger, int count, string sources);

    [LoggerMessage(EventId = 4100, Level = LogLevel.Error, Message = "Events intake: the poll loop failed; it retries on its next turn.")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);
}
