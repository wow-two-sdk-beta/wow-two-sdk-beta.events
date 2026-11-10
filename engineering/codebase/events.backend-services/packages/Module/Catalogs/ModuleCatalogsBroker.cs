using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Catalogs;

/// <summary>Integrates catalogs in process. STUB: replaced by its implementation.</summary>
internal sealed class ModuleCatalogsBroker : IEventCatalogsBroker
{
    /// <inheritdoc />
    public Task<Result<CatalogModel>> GetAsync(string slug, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<CatalogModel>>> ListAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<CatalogModel>> PutAsync(string slug, CatalogSpec spec, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
