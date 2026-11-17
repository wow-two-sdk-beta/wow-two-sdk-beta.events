namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_digest_runs</c>: one occurrence a digest ran for, and what it rendered.</summary>
internal sealed class DigestRunEntity
{
    public Guid Id { get; set; }
    public Guid DigestId { get; set; }
    public DateTimeOffset OccurrenceAt { get; set; }
    public string Trigger { get; set; } = "schedule";
    public string Status { get; set; } = "running";
    public int Matched { get; set; }
    public int Selected { get; set; }
    public string EventIds { get; set; } = "[]";
    public string Items { get; set; } = "[]";
    public string Texts { get; set; } = "[]";
    public string Notes { get; set; } = "[]";
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}
