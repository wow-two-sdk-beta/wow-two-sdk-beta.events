using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Calendar;

/// <summary>Integrates calendar export in process. STUB: replaced by its implementation.</summary>
internal sealed class ModuleCalendarBroker : IEventCalendarBroker
{
    /// <inheritdoc />
    public Task<Result<CalendarFile>> ExportAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
