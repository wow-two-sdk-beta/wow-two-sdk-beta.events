namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_catalogs</c>.</summary>
internal sealed class CatalogEntity
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerIssuer { get; set; } = string.Empty;
    public string OwnerOrganizationId { get; set; } = string.Empty;
    public string Visibility { get; set; } = "private";
    public string Locales { get; set; } = "[]";
    public string Kinds { get; set; } = "[]";
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
