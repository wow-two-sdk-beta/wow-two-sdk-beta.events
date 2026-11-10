using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Tracking;

/// <summary>Integrates the running Events service's tracking, as the caller's active organization.</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiEventTrackingBroker(EventsApiTransport transport) : IEventTrackingBroker
{
    /// <summary>Holds the summary's segment under a catalog's tracking.</summary>
    private const string Summary = "summary";

    /// <summary>Holds the history's segment under one event's tracking.</summary>
    private const string History = "history";

    /// <inheritdoc />
    public Task<Result<TrackingPage>> ListAsync(string catalog, TrackingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var route = new EventsApiQuery()
            .Add("trackedOnly", query.TrackedOnly ? true : null)
            .Add("stage", query.Stage)
            .AddEnum("priority", query.Priority)
            .Add("from", query.From)
            .Add("to", query.To)
            .Add("kind", query.Kind)
            .Add("country", query.Country)
            .Add("text", query.Text)
            .Add("deadlineBefore", query.DeadlineBefore)
            .Add("includeUndated", query.IncludeUndated ? null : false)
            .Add("cursor", query.Cursor)
            .Add("limit", query.Limit)
            .ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Tracking));
        return transport.SendAsync<TrackingPage>(HttpMethod.Get, route, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<TrackedEventModel>> GetAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default) =>
        transport.SendAsync<TrackedEventModel>(HttpMethod.Get, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Tracking, eventId), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TrackingModel>> PutAsync(string catalog, Guid eventId, TrackingSpec spec, CancellationToken cancellationToken = default) =>
        transport.SendAsync<TrackingSpec, TrackingModel>(HttpMethod.Put, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Tracking, eventId), spec, cancellationToken);

    /// <inheritdoc />
    public Task<Result<TrackingModel>> DeleteAsync(string catalog, Guid eventId, int expectedRevision, CancellationToken cancellationToken = default) =>
        transport.SendAsync<TrackingModel>(
            HttpMethod.Delete,
            new EventsApiQuery().Add("expectedRevision", expectedRevision).ToRoute(EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Tracking, eventId)),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<TrackingHistoryEntryModel>>> GetHistoryAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default) =>
        transport.SendAsync<IReadOnlyList<TrackingHistoryEntryModel>>(HttpMethod.Get, EventsApiRoutes.Catalog(catalog, EventsRouteConstants.Tracking, eventId, History), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TrackingSummaryModel>> GetSummaryAsync(string catalog, DateOnly today, CancellationToken cancellationToken = default) =>
        transport.SendAsync<TrackingSummaryModel>(
            HttpMethod.Get,
            new EventsApiQuery().Add("today", today).ToRoute(EventsApiRoutes.Catalog(catalog, $"{EventsRouteConstants.Tracking}/{Summary}")),
            cancellationToken);
}
