using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Integrates scheduled event digests of a catalog, kept per organization: the definitions, a preview, a send now
/// and the run history, in process or over the service.</summary>
/// <remarks>
///   - every call acts for the current principal's active organization, which must read the catalog; another
///     organization's digests answer <see cref="EventsErrors.DigestNotFound"/>
///   - the schedule runs each occurrence once (a run per digest and occurrence) through the digest's channel
///     (<see cref="IDigestDelivery"/>), one message per recipient language
/// </remarks>
public interface IEventDigestsBroker
{
    /// <summary>Lists the organization's digests of a catalog, by name.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The digests; <see cref="EventsErrors.CatalogNotFound"/> when the caller cannot read the catalog.</returns>
    Task<Result<IReadOnlyList<DigestModel>>> ListAsync(string catalog, CancellationToken cancellationToken = default);

    /// <summary>Gets one digest.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest's id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The digest; <see cref="EventsErrors.DigestNotFound"/> when the organization holds none with that id.</returns>
    Task<Result<DigestModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a digest for the caller's organization.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="spec">The digest's fields.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The digest; a validation failure for an invalid field or an unregistered channel.</returns>
    Task<Result<DigestModel>> CreateAsync(string catalog, DigestSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Replaces a digest at the revision read; a changed schedule runs only occurrences after the change.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest's id.</param>
    /// <param name="spec">The digest's fields, with <see cref="DigestSpec.ExpectedRevision"/>.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The digest; <see cref="EventsErrors.RevisionConflict"/> when the revision is stale.</returns>
    Task<Result<DigestModel>> UpdateAsync(string catalog, Guid id, DigestSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Deletes a digest and its run history.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest's id.</param>
    /// <param name="expectedRevision">The revision the caller read.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The deleted digest; <see cref="EventsErrors.RevisionConflict"/> when the revision is stale.</returns>
    Task<Result<DigestModel>> DeleteAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default);

    /// <summary>Renders the digest now without sending or writing anything: the texts a send now at this instant sends.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest's id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The preview.</returns>
    Task<Result<DigestPreviewModel>> PreviewAsync(string catalog, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Runs the digest now, outside its schedule, and sends it to every recipient.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest's id.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The run with each recipient's result; a passing failure stays <c>pending</c> and is retried.</returns>
    Task<Result<DigestRunModel>> SendNowAsync(string catalog, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists the digest's runs, newest first.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The digest's id.</param>
    /// <param name="query">The page.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>One page of runs.</returns>
    Task<Result<DigestRunPage>> ListRunsAsync(string catalog, Guid id, DigestRunQuery query, CancellationToken cancellationToken = default);
}
