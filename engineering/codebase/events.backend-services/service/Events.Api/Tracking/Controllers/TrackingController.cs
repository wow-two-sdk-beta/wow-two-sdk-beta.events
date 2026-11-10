using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Shared;
using Events.Api.Tracking.Models;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Tracking.Controllers;

/// <summary>Exposes <see cref="IEventTrackingBroker"/> for the caller's active organization: reads take
/// <c>events.read</c>, writes <c>events.track</c>.</summary>
/// <param name="tracking">The tracking broker.</param>
/// <param name="time">The clock a summary without a day counts from.</param>
[Route(EventsRouteConstants.Catalogs + "/{catalog}/" + EventsRouteConstants.Tracking)]
public sealed class TrackingController(IEventTrackingBroker tracking, TimeProvider time) : EventsControllerBase
{
    /// <summary>Lists the catalog's events with the organization's tracking.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="parameters">The filters and the page.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>One page of tracked events.</returns>
    [HttpGet]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<TrackingPage>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(string catalog, [FromQuery] TrackingQueryParameters parameters, CancellationToken ct) =>
        parameters.TryMap(out var query) is { } error ? Invalid(error) : Answer(await tracking.ListAsync(catalog, query, ct));

    /// <summary>Summarizes the organization's tracking: counts per stage, the nearest open deadlines.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="today">The first day a deadline may fall on, <c>yyyy-MM-dd</c>; the UTC day when absent.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The summary.</returns>
    [HttpGet("summary")]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<TrackingSummaryModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(string catalog, [FromQuery] string? today, CancellationToken ct)
    {
        if (WireQueryMapper.TryDay("today", today, out var day) is { } error)
            return Invalid(error);

        return Answer(await tracking.GetSummaryAsync(catalog, day ?? DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), ct));
    }

    /// <summary>Gets an event with the organization's tracking.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The event and its tracking.</returns>
    [HttpGet("{eventId:guid}")]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<TrackedEventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string catalog, Guid eventId, CancellationToken ct) => Answer(await tracking.GetAsync(catalog, eventId, ct));

    /// <summary>Creates or replaces the organization's tracking of an event.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="spec">The tracking's fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The tracking.</returns>
    [HttpPut("{eventId:guid}")]
    [Authorize(Policy = EventsScopeConstants.Track)]
    [ProducesResponseType<ApiResponse<TrackingModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Put(string catalog, Guid eventId, [FromBody] TrackingSpec spec, CancellationToken ct) =>
        Answer(await tracking.PutAsync(catalog, eventId, spec, ct));

    /// <summary>Removes the organization's tracking of an event.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The removed tracking.</returns>
    [HttpDelete("{eventId:guid}")]
    [Authorize(Policy = EventsScopeConstants.Track)]
    [ProducesResponseType<ApiResponse<TrackingModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(string catalog, Guid eventId, [FromQuery] int? expectedRevision, CancellationToken ct) =>
        WireQueryMapper.RequireRevision(expectedRevision, out var revision) is { } error
            ? Invalid(error)
            : Answer(await tracking.DeleteAsync(catalog, eventId, revision, ct));

    /// <summary>Lists the stage changes of the organization's tracking of an event.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The history.</returns>
    [HttpGet("{eventId:guid}/history")]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TrackingHistoryEntryModel>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> History(string catalog, Guid eventId, CancellationToken ct) =>
        Answer(await tracking.GetHistoryAsync(catalog, eventId, ct));
}
