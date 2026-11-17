using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Runs the digests' scheduler every <see cref="EventDigestsOptions.PollInterval"/> while
/// <see cref="EventDigestsOptions.Enabled"/>; never crashes the host.</summary>
/// <param name="options">The digests' settings.</param>
/// <param name="pass">One turn.</param>
/// <param name="time">The clock the interval runs on.</param>
/// <param name="logger">The log.</param>
internal sealed partial class DigestScheduler(
    IOptionsMonitor<EventDigestsOptions> options,
    DigestSchedulerPass pass,
    TimeProvider time,
    ILogger<DigestScheduler> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        LogStarted(logger, options.CurrentValue.Enabled, options.CurrentValue.PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.CurrentValue.Enabled)
            {
                try
                {
                    await pass.RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    LogTurnFailed(logger, exception);
                }
            }

            try
            {
                var wait = options.CurrentValue.PollInterval;
                await Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.FromSeconds(30), time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(EventId = 4311, Level = LogLevel.Information, Message = "Events digests: scheduler on: {Enabled}, every {Interval}.")]
    private static partial void LogStarted(ILogger logger, bool enabled, TimeSpan interval);

    [LoggerMessage(EventId = 4312, Level = LogLevel.Error, Message = "Events digests: the scheduler's turn failed; it retries on its next turn.")]
    private static partial void LogTurnFailed(ILogger logger, Exception exception);
}
