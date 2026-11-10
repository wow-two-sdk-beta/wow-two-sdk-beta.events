namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_tracking</c>: one organization's record about one event.</summary>
internal sealed class TrackingEntity
{
    public Guid Id { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
    public string? Assignee { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string? Fields { get; set; }
    public string Refs { get; set; } = "[]";
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
