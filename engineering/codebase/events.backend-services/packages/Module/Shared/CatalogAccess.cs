using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Finds a catalog and applies its record rules: who reads it, and who owns it (events-integration #23).</summary>
internal static class CatalogAccess
{
    /// <summary>Finds a catalog the caller may read: its owner organization, or anyone authenticated for a public one.
    /// A shared catalog reads as private until sharing lands (P2).</summary>
    /// <returns>The catalog; <see cref="EventsErrors.CatalogNotFound"/> when the caller cannot see it.</returns>
    public static async Task<Result<CatalogEntity>> FindReadableAsync(EventsDbContext db, EventsCaller caller, string slug, CancellationToken cancellationToken)
    {
        var catalog = await db.Catalogs.AsNoTracking().FirstOrDefaultAsync(row => row.Slug == slug, cancellationToken);
        return catalog is not null && CanRead(catalog, caller)
            ? Result<CatalogEntity>.Ok(catalog)
            : Result<CatalogEntity>.Fail(EventsErrors.CatalogNotFound());
    }

    /// <summary>Finds a catalog the caller's organization owns.</summary>
    /// <returns>The catalog; <see cref="EventsErrors.CatalogNotFound"/> when the caller cannot see it;
    /// <see cref="EventsErrors.NotCatalogOwner"/> when it can read but not own it.</returns>
    public static async Task<Result<CatalogEntity>> FindOwnedAsync(EventsDbContext db, EventsCaller caller, string slug, CancellationToken cancellationToken)
    {
        var readable = await FindReadableAsync(db, caller, slug, cancellationToken);
        if (readable is not Result<CatalogEntity>.Success { Value: var catalog })
            return readable;

        return IsOwner(catalog, caller) ? Result<CatalogEntity>.Ok(catalog) : Result<CatalogEntity>.Fail(EventsErrors.NotCatalogOwner());
    }

    /// <summary>Tells whether the caller may read the catalog.</summary>
    public static bool CanRead(CatalogEntity catalog, EventsCaller caller) =>
        IsOwner(catalog, caller)
        || (catalog.Visibility == EventsLabels.Of(CatalogVisibility.Public) && caller.Kind != Identity.CurrentPrincipal.PrincipalKind.Anonymous);

    /// <summary>Tells whether the caller acts for the catalog's owner organization.</summary>
    public static bool IsOwner(CatalogEntity catalog, EventsCaller caller) =>
        caller.IsOrganization(catalog.OwnerIssuer, catalog.OwnerOrganizationId);

    /// <summary>Gets the catalog's kind keys.</summary>
    public static IReadOnlyCollection<string> KindKeys(CatalogEntity catalog) =>
        EventsJson.Read<List<CatalogKindModel>>(catalog.Kinds).Select(kind => kind.Key).ToHashSet(StringComparer.Ordinal);
}
