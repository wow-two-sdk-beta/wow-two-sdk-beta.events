namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_intake_cursors</c>: one source's committed cursor and freshness.</summary>
internal sealed class IntakeCursorEntity
{
    public string SourceName { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string? Cursor { get; set; }
    public DateTimeOffset? LastPolledAt { get; set; }
    public DateTimeOffset? LastReceivedAt { get; set; }
    public DateTimeOffset? NewestObservedAt { get; set; }
    public string? LastError { get; set; }
    public int ConsecutiveFailures { get; set; }
    public long Received { get; set; }
    public long Quarantined { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
