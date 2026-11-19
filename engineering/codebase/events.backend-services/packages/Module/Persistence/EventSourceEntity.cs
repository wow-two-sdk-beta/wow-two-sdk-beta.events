namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_event_sources</c>: one source identity behind an event.</summary>
internal sealed class EventSourceEntity
{
    public Guid CatalogId { get; set; }
    public string Kind { get; set; } = "scfd";
    public string SourceId { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? Name { get; set; }
    public string? State { get; set; }
    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>Gets or sets the rule that attached the source to an event it did not start; none for the source the event
    /// started from, an import's and a curator's.</summary>
    public string? MatchedBy { get; set; }
}
