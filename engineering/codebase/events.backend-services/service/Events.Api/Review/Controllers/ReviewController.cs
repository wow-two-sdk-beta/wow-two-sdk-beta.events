using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Review.Models;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Review.Controllers;

/// <summary>Exposes <see cref="IEventReviewBroker"/>: the queue and the observations, each <c>events.review</c>.</summary>
/// <param name="review">The review broker.</param>
[Route(EventsRouteConstants.Catalogs + "/{catalog}")]
[Authorize(Policy = EventsScopeConstants.Review)]
public sealed class ReviewController(IEventReviewBroker review) : EventsControllerBase
{
    /// <summary>Lists the review queue: candidates and events with pending changes.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="cursor">The page's cursor.</param>
    /// <param name="limit">The page's size.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>One page of the queue.</returns>
    [HttpGet(EventsRouteConstants.Review)]
    [ProducesResponseType<ApiResponse<ReviewPage>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(string catalog, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken ct) =>
        Answer(await review.ListAsync(catalog, new ReviewQuery { Cursor = cursor, Limit = limit ?? EventsConstants.DefaultPageSize }, ct));

    /// <summary>Accepts a candidate: it publishes, with the reviewer's edits.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The candidate.</param>
    /// <param name="decision">The revision read and optional edits.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The published event.</returns>
    [HttpPost(EventsRouteConstants.Review + "/{id:guid}/accept")]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(string catalog, Guid id, [FromBody] ReviewDecision decision, CancellationToken ct) =>
        Answer(await review.AcceptAsync(catalog, id, decision, ct));

    /// <summary>Rejects a candidate.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The candidate.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The rejected event.</returns>
    [HttpPost(EventsRouteConstants.Review + "/{id:guid}/reject")]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(string catalog, Guid id, [FromQuery] int? expectedRevision, CancellationToken ct) =>
        WireQueryMapper.RequireRevision(expectedRevision, out var revision) is { } error
            ? Invalid(error)
            : Answer(await review.RejectAsync(catalog, id, revision, ct));

    /// <summary>Returns a published event to the review queue as a candidate.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The published event.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The candidate.</returns>
    [HttpPost(EventsRouteConstants.Review + "/{id:guid}/reopen")]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reopen(string catalog, Guid id, [FromQuery] int? expectedRevision, CancellationToken ct) =>
        WireQueryMapper.RequireRevision(expectedRevision, out var revision) is { } error
            ? Invalid(error)
            : Answer(await review.ReopenAsync(catalog, id, revision, ct));

    /// <summary>Merges a candidate into an existing event: sources and evidence move, the candidate archives.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The candidate.</param>
    /// <param name="decision">The target and both revisions read.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The target event.</returns>
    [HttpPost(EventsRouteConstants.Review + "/{id:guid}/merge")]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Merge(string catalog, Guid id, [FromBody] MergeDecision decision, CancellationToken ct) =>
        Answer(await review.MergeAsync(catalog, id, decision, ct));

    /// <summary>Applies a pending change to its published event.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The pending observation.</param>
    /// <param name="decision">The event's revision read and optional edits.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The changed event.</returns>
    [HttpPost(EventsRouteConstants.Observations + "/{id:guid}/apply")]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Apply(string catalog, Guid id, [FromBody] ReviewDecision decision, CancellationToken ct) =>
        Answer(await review.ApplyChangeAsync(catalog, id, decision, ct));

    /// <summary>Dismisses a pending change.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The pending observation.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The dismissed observation.</returns>
    [HttpPost(EventsRouteConstants.Observations + "/{id:guid}/dismiss")]
    [ProducesResponseType<ApiResponse<ObservationModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dismiss(string catalog, Guid id, CancellationToken ct) =>
        Answer(await review.DismissChangeAsync(catalog, id, ct));

    /// <summary>Lists observations, newest first.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="parameters">The filters and the page.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>One page of observations.</returns>
    [HttpGet(EventsRouteConstants.Observations)]
    [ProducesResponseType<ApiResponse<ObservationPage>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListObservations(string catalog, [FromQuery] ObservationQueryParameters parameters, CancellationToken ct) =>
        parameters.TryMap(out var query) is { } error ? Invalid(error) : Answer(await review.ListObservationsAsync(catalog, query, ct));

    /// <summary>Gets an observation.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The observation.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The observation.</returns>
    [HttpGet(EventsRouteConstants.Observations + "/{id:guid}")]
    [ProducesResponseType<ApiResponse<ObservationModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetObservation(string catalog, Guid id, CancellationToken ct) =>
        Answer(await review.GetObservationAsync(catalog, id, ct));
}
