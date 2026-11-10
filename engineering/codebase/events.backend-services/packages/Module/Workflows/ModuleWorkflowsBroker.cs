using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Workflows;

/// <summary>Integrates workflows in process. STUB: replaced by its implementation.</summary>
internal sealed class ModuleWorkflowsBroker : IEventWorkflowsBroker
{
    /// <inheritdoc />
    public Task<Result<WorkflowModel>> GetAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<WorkflowModel>> PutAsync(WorkflowSpec spec, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
