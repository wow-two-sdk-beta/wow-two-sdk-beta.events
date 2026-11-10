using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Catalogs;

/// <summary>Integrates Events' catalogs — read, create and change them — in process or over the service.</summary>
/// <remarks>Every call acts for the current principal: it sees the catalogs its organization owns and the public ones.</remarks>
public interface IEventCatalogsBroker
{
    /// <summary>Gets a catalog.</summary>
    /// <param name="slug">The catalog's slug.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The catalog; <see cref="EventsErrors.CatalogNotFound"/> when the caller cannot see it.</returns>
    Task<Result<CatalogModel>> GetAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Lists the catalogs the caller can see, by slug.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The catalogs.</returns>
    Task<Result<IReadOnlyList<CatalogModel>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a catalog owned by the caller's organization, or replaces one it owns.</summary>
    /// <param name="slug">The catalog's slug, matching <see cref="EventsConstants.KeyPattern"/>.</param>
    /// <param name="spec">The catalog's fields.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The catalog; <see cref="EventsErrors.NotCatalogOwner"/> when another organization owns the slug;
    /// <see cref="EventsErrors.RevisionConflict"/> when the expected revision is stale.</returns>
    Task<Result<CatalogModel>> PutAsync(string slug, CatalogSpec spec, CancellationToken cancellationToken = default);
}
