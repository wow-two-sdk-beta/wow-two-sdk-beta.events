namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_tracking_history</c>: one stage change.</summary>
internal sealed class TrackingHistoryEntity
{
    public Guid Id { get; set; }
    public Guid TrackingId { get; set; }
    public string? FromStage { get; set; }
    public string ToStage { get; set; } = string.Empty;
    public string? ActorIssuer { get; set; }
    public string? ActorSubject { get; set; }
    public DateTimeOffset At { get; set; }
}
