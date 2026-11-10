namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_observations</c>: one delivered record and its evidence.</summary>
internal sealed class ObservationEntity
{
    public Guid Id { get; set; }
    public long Seq { get; set; }
    public Guid CatalogId { get; set; }
    public string SourceKind { get; set; } = "scfd";
    public string? SourceInstance { get; set; }
    public string? SubscriptionId { get; set; }
    public string? PipelineId { get; set; }
    public Guid? DeliveryEventId { get; set; }
    public string? ItemId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public Guid? PostId { get; set; }
    public int? Revision { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? MappingVersion { get; set; }
    public string Links { get; set; } = "[]";
    public string? Payload { get; set; }
    public string? Proposed { get; set; }
    public string State { get; set; } = "recorded";
    public string? QuarantineReason { get; set; }
    public Guid? EventId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedByIssuer { get; set; }
    public string? ResolvedBySubject { get; set; }
}
