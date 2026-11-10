using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Workflows;

/// <summary>Integrates the running Events service's workflow of the caller's active organization.</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiEventWorkflowsBroker(EventsApiTransport transport) : IEventWorkflowsBroker
{
    /// <inheritdoc />
    public Task<Result<WorkflowModel>> GetAsync(CancellationToken cancellationToken = default) =>
        transport.SendAsync<WorkflowModel>(HttpMethod.Get, EventsRouteConstants.Workflow, cancellationToken);

    /// <inheritdoc />
    public Task<Result<WorkflowModel>> PutAsync(WorkflowSpec spec, CancellationToken cancellationToken = default) =>
        transport.SendAsync<WorkflowSpec, WorkflowModel>(HttpMethod.Put, EventsRouteConstants.Workflow, spec, cancellationToken);
}
