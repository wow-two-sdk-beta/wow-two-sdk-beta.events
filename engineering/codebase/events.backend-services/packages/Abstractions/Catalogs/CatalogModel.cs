namespace WoW.Two.Sdk.Beta.Be.Events.Catalogs;

/// <summary>Represents a catalog: a curated namespace of events with its own kinds and reviewers.</summary>
public sealed record CatalogModel
{
    /// <summary>Gets the catalog's slug, unique per instance.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the catalog's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the issuer of the owner organization.</summary>
    public required string OwnerIssuer { get; init; }

    /// <summary>Gets the owner organization's id at its issuer.</summary>
    public required string OwnerOrganizationId { get; init; }

    /// <summary>Gets who reads the catalog's published events.</summary>
    public required CatalogVisibility Visibility { get; init; }

    /// <summary>Gets the locales the catalog's texts are written in.</summary>
    public IReadOnlyList<string> Locales { get; init; } = [];

    /// <summary>Gets the catalog's kinds; an empty list accepts any kind key.</summary>
    public IReadOnlyList<CatalogKindModel> Kinds { get; init; } = [];

    /// <summary>Gets the revision a write must name.</summary>
    public required int Revision { get; init; }

    /// <summary>Gets when the catalog was created (UTC).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Gets when the catalog last changed (UTC).</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
