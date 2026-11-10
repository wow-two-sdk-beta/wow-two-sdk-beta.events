using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Intake;

/// <summary>Integrates the intake's operations — its status and an on-demand poll — in process or over the service.</summary>
/// <remarks>Operator calls: the service gates them with <c>events.admin</c>. The background poller runs without them.</remarks>
public interface IEventIntakeBroker
{
    /// <summary>Gets the intake's status.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The status.</returns>
    Task<Result<IntakeStatusModel>> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Polls every enabled source now, until each answers an empty page or a page limit, then reports.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The status after the poll.</returns>
    Task<Result<IntakeStatusModel>> PollAsync(CancellationToken cancellationToken = default);
}
