using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Integrates tracking in process. STUB: replaced by its implementation.</summary>
internal sealed class ModuleTrackingBroker : IEventTrackingBroker
{
    /// <inheritdoc />
    public Task<Result<TrackingPage>> ListAsync(string catalog, TrackingQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<TrackedEventModel>> GetAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<TrackingModel>> PutAsync(string catalog, Guid eventId, TrackingSpec spec, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<TrackingModel>> DeleteAsync(string catalog, Guid eventId, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<TrackingHistoryEntryModel>>> GetHistoryAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<TrackingSummaryModel>> GetSummaryAsync(string catalog, DateOnly today, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
