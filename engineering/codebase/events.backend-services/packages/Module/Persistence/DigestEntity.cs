namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_digests</c>: one organization's digest of one catalog.</summary>
internal sealed class DigestEntity
{
    public Guid Id { get; set; }
    public Guid CatalogId { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Schedule { get; set; } = "{}";
    public string Selection { get; set; } = "{}";
    public string Recipients { get; set; } = "[]";
    public string Language { get; set; } = string.Empty;
    public int ItemCap { get; set; }
    public string? LinkTemplate { get; set; }
    public DateTimeOffset ScheduleSince { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
