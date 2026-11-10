using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.CatalogEvents.Models;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.CatalogEvents.Controllers;

/// <summary>Exposes <see cref="ICatalogEventsBroker"/>: reads take <c>events.read</c>, a curator's writes
/// <c>events.review</c>, a member's proposal <c>events.track</c>.</summary>
/// <param name="events">The catalog events broker.</param>
[Route(EventsRouteConstants.Catalogs + "/{catalog}")]
public sealed class CatalogEventsController(ICatalogEventsBroker events) : EventsControllerBase
{
    /// <summary>Lists a catalog's events: published ones for a reader, every state for the owner.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="parameters">The filters and the page.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>One page of events.</returns>
    [HttpGet(EventsRouteConstants.Events)]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<EventPage>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(string catalog, [FromQuery] EventQueryParameters parameters, CancellationToken ct) =>
        parameters.TryMap(out var query) is { } error ? Invalid(error) : Answer(await events.ListAsync(catalog, query, ct));

    /// <summary>Gets an event.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The event.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The event.</returns>
    [HttpGet(EventsRouteConstants.Events + "/{id:guid}")]
    [Authorize(Policy = EventsScopeConstants.Read)]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string catalog, Guid id, CancellationToken ct) => Answer(await events.GetAsync(catalog, id, ct));

    /// <summary>Creates a published event by hand: a curator's entry is reviewed by definition.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="spec">The facts.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The event, <c>201</c>.</returns>
    [HttpPost(EventsRouteConstants.Events)]
    [Authorize(Policy = EventsScopeConstants.Review)]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(string catalog, [FromBody] EventSpec spec, CancellationToken ct) =>
        AnswerCreated(await events.CreateAsync(catalog, spec, ct), created => Location(catalog, created.Id));

    /// <summary>Proposes an event: a member's entry waits for a reviewer as a candidate.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="spec">The facts.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The candidate, <c>201</c>.</returns>
    [HttpPost(EventsRouteConstants.Candidates)]
    [Authorize(Policy = EventsScopeConstants.Track)]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Propose(string catalog, [FromBody] EventSpec spec, CancellationToken ct) =>
        AnswerCreated(await events.ProposeAsync(catalog, spec, ct), created => Location(catalog, created.Id));

    /// <summary>Replaces an event's facts.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The event.</param>
    /// <param name="spec">The facts and the revision read.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The event.</returns>
    [HttpPut(EventsRouteConstants.Events + "/{id:guid}")]
    [Authorize(Policy = EventsScopeConstants.Review)]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string catalog, Guid id, [FromBody] EventSpec spec, CancellationToken ct) =>
        Answer(await events.UpdateAsync(catalog, id, spec, ct));

    /// <summary>Archives an event.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The event.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The archived event.</returns>
    [HttpDelete(EventsRouteConstants.Events + "/{id:guid}")]
    [Authorize(Policy = EventsScopeConstants.Review)]
    [ProducesResponseType<ApiResponse<EventModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(string catalog, Guid id, [FromQuery] int? expectedRevision, CancellationToken ct) =>
        WireQueryMapper.RequireRevision(expectedRevision, out var revision) is { } error
            ? Invalid(error)
            : Answer(await events.ArchiveAsync(catalog, id, revision, ct));

    private static string Location(string catalog, Guid id) =>
        $"/{EventsRouteConstants.Catalogs}/{Uri.EscapeDataString(catalog)}/{EventsRouteConstants.Events}/{id:D}";
}
