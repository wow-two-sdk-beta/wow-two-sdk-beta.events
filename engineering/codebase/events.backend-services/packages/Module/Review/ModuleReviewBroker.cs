using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Review;

/// <summary>Integrates review in process. STUB: replaced by its implementation.</summary>
internal sealed class ModuleReviewBroker : IEventReviewBroker
{
    /// <inheritdoc />
    public Task<Result<ReviewPage>> ListAsync(string catalog, ReviewQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<EventModel>> AcceptAsync(string catalog, Guid id, ReviewDecision decision, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<EventModel>> RejectAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<EventModel>> MergeAsync(string catalog, Guid id, MergeDecision decision, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<EventModel>> ApplyChangeAsync(string catalog, Guid observationId, ReviewDecision decision, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<ObservationModel>> DismissChangeAsync(string catalog, Guid observationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<ObservationPage>> ListObservationsAsync(string catalog, ObservationQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<ObservationModel>> GetObservationAsync(string catalog, Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
