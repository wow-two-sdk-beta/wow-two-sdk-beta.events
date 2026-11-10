namespace WoW.Two.Sdk.Beta.Be.Events.Catalogs;

/// <summary>Describes a catalog to create or replace; the caller's organization becomes the owner of a new one.</summary>
public sealed record CatalogSpec
{
    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets who reads the catalog's published events.</summary>
    public CatalogVisibility Visibility { get; init; } = CatalogVisibility.Private;

    /// <summary>Gets the locales the catalog's texts are written in.</summary>
    public IReadOnlyList<string> Locales { get; init; } = [];

    /// <summary>Gets the catalog's kinds.</summary>
    public IReadOnlyList<CatalogKindModel> Kinds { get; init; } = [];

    /// <summary>Gets the revision the caller read; none when creating.</summary>
    public int? ExpectedRevision { get; init; }
}
