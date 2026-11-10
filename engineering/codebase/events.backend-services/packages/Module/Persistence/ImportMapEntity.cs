namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_import_map</c>: one imported row's old and new ids.</summary>
internal sealed class ImportMapEntity
{
    public Guid CatalogId { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string LegacyId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public Guid? TrackingId { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
