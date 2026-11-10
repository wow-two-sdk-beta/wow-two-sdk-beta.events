namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_event_deadlines</c>: one deadline's date span.</summary>
internal sealed class EventDeadlineEntity
{
    public Guid EventId { get; set; }
    public int Ordinal { get; set; }
    public string Kind { get; set; } = "other";
    public DateOnly? AtMin { get; set; }
    public DateOnly? AtMax { get; set; }
}
