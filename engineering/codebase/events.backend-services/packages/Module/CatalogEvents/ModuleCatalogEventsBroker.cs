using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.CatalogEvents;

/// <summary>Integrates a catalog's events in process. STUB: replaced by its implementation.</summary>
internal sealed class ModuleCatalogEventsBroker : ICatalogEventsBroker
{
    /// <inheritdoc />
    public Task<Result<EventPage>> ListAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<EventModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<EventModel>> CreateAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<EventModel>> UpdateAsync(string catalog, Guid id, EventSpec spec, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<EventModel>> ArchiveAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
