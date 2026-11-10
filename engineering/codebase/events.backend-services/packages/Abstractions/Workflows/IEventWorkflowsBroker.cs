using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Workflows;

/// <summary>Integrates the caller organization's workflow, in process or over the service.</summary>
/// <remarks>An organization that never wrote one reads the default: <c>interested</c>, <c>planning</c>, <c>going</c>,
/// <c>attended</c> (won), <c>skipped</c> (lost). A stage still in use cannot be removed.</remarks>
public interface IEventWorkflowsBroker
{
    /// <summary>Gets the organization's workflow.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The workflow.</returns>
    Task<Result<WorkflowModel>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the organization's workflow.</summary>
    /// <param name="spec">The stages, the default stage and the fields.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The workflow; a validation failure when a removed stage is still in use.</returns>
    Task<Result<WorkflowModel>> PutAsync(WorkflowSpec spec, CancellationToken cancellationToken = default);
}
