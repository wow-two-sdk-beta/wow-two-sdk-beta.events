namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_catalog_events</c>: the facts as jsonb, and the columns lists filter and sort on.</summary>
internal sealed class CatalogEventEntity
{
    public Guid Id { get; set; }
    public Guid CatalogId { get; set; }
    public string? Key { get; set; }
    public string Review { get; set; } = "candidate";
    public Guid? MergedInto { get; set; }
    public string Facts { get; set; } = "{}";
    public string TitleSort { get; set; } = string.Empty;
    public string Search { get; set; } = string.Empty;
    public string Kind { get; set; } = "other";
    public string Status { get; set; } = "scheduled";
    public string? Country { get; set; }
    public string? City { get; set; }
    public DateOnly? StartsMin { get; set; }
    public DateOnly? StartsMax { get; set; }
    public DateOnly? EndsMax { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
