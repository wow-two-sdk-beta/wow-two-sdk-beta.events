using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Imports;

/// <summary>Integrates imports in process. STUB: replaced by its implementation.</summary>
internal sealed class ModuleImportsBroker : IEventImportsBroker
{
    /// <inheritdoc />
    public Task<Result<EventImportReport>> ImportAsync(string catalog, EventImportBundle bundle, bool dryRun, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
