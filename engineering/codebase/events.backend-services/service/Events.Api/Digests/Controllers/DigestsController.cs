using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Events.Api.Shared;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;

namespace Events.Api.Digests.Controllers;

/// <summary>Exposes <see cref="IEventDigestsBroker"/> for the caller's active organization: every action takes
/// <c>events.admin</c>, and a person's <c>events:admin</c>.</summary>
/// <param name="digests">The digests broker.</param>
[Route(EventsRouteConstants.Catalogs + "/{catalog}/" + EventsRouteConstants.Digests)]
[Authorize(Policy = EventsScopeConstants.Admin)]
public sealed class DigestsController(IEventDigestsBroker digests) : EventsControllerBase
{
    /// <summary>Lists the organization's digests of the catalog.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The digests.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DigestModel>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(string catalog, CancellationToken ct) => Answer(await digests.ListAsync(catalog, ct));

    /// <summary>Gets a digest.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The digest.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<DigestModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string catalog, Guid id, CancellationToken ct) => Answer(await digests.GetAsync(catalog, id, ct));

    /// <summary>Creates a digest.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="spec">The digest's fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The digest, <c>201</c>.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<DigestModel>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(string catalog, [FromBody] DigestSpec spec, CancellationToken ct) =>
        AnswerCreated(await digests.CreateAsync(catalog, spec, ct), digest => $"/{EventsRouteConstants.Catalogs}/{Uri.EscapeDataString(catalog)}/{EventsRouteConstants.Digests}/{digest.Id:D}");

    /// <summary>Replaces a digest at the revision read.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest.</param>
    /// <param name="spec">The digest's fields, with the revision read.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The digest.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<DigestModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string catalog, Guid id, [FromBody] DigestSpec spec, CancellationToken ct) =>
        Answer(await digests.UpdateAsync(catalog, id, spec, ct));

    /// <summary>Deletes a digest and its run history.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The deleted digest.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<ApiResponse<DigestModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(string catalog, Guid id, [FromQuery] int expectedRevision, CancellationToken ct) =>
        Answer(await digests.DeleteAsync(catalog, id, expectedRevision, ct));

    /// <summary>Renders the digest now without sending.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The preview.</returns>
    [HttpPost("{id:guid}/preview")]
    [ProducesResponseType<ApiResponse<DigestPreviewModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(string catalog, Guid id, CancellationToken ct) => Answer(await digests.PreviewAsync(catalog, id, ct));

    /// <summary>Runs the digest now and sends it.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>The run with each recipient's result.</returns>
    [HttpPost("{id:guid}/send")]
    [ProducesResponseType<ApiResponse<DigestRunModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(string catalog, Guid id, CancellationToken ct) => Answer(await digests.SendNowAsync(catalog, id, ct));

    /// <summary>Lists the digest's runs, newest first.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest.</param>
    /// <param name="cursor">The cursor a previous page returned.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>One page of runs.</returns>
    [HttpGet("{id:guid}/runs")]
    [ProducesResponseType<ApiResponse<DigestRunPage>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Runs(string catalog, Guid id, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken ct) =>
        Answer(await digests.ListRunsAsync(catalog, id, new DigestRunQuery { Cursor = cursor, Limit = limit ?? 20 }, ct));
}
