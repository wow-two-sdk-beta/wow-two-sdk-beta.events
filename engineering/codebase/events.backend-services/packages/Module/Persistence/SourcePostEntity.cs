namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_source_posts</c>: the newest revision applied per post.</summary>
internal sealed class SourcePostEntity
{
    public string SourceInstance { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public Guid PostId { get; set; }
    public Guid CatalogId { get; set; }
    public Guid? EventId { get; set; }
    public int LatestRevision { get; set; }
    public Guid LatestObservationId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
