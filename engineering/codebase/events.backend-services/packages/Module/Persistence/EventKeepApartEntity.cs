namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_event_keep_apart</c>: a source identity a reviewer split off an event, which intake
/// never attaches to that event again.</summary>
internal sealed class EventKeepApartEntity
{
    public Guid CatalogId { get; set; }
    public Guid EventId { get; set; }
    public string SourceKind { get; set; } = "scfd";
    public string SourceId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
