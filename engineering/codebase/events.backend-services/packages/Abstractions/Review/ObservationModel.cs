using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Represents one record a source delivered, with its evidence and what intake did with it.</summary>
public sealed record ObservationModel
{
    /// <summary>Gets the observation's id, a UUID v7.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the slug of the catalog it was delivered into.</summary>
    public required string Catalog { get; init; }

    /// <summary>Gets how the source reported it.</summary>
    public required EventSourceKind SourceKind { get; init; }

    /// <summary>Gets the SCFD instance's name, as configured.</summary>
    public string? SourceInstance { get; init; }

    /// <summary>Gets the SCFD subscription.</summary>
    public string? SubscriptionId { get; init; }

    /// <summary>Gets the SCFD pipeline.</summary>
    public string? PipelineId { get; init; }

    /// <summary>Gets the delivery's id; a retry carries the same one.</summary>
    public Guid? DeliveryEventId { get; init; }

    /// <summary>Gets the content's id across subscriptions and resyncs.</summary>
    public string? ItemId { get; init; }

    /// <summary>Gets the delivery's type, such as <c>post.new</c> or <c>link.state</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Gets why SCFD delivered it: <c>ingest</c>, <c>backfill</c> or <c>resync</c>.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the post the record is about.</summary>
    public Guid? PostId { get; init; }

    /// <summary>Gets the post's revision.</summary>
    public int? Revision { get; init; }

    /// <summary>Gets when the source observed it (UTC).</summary>
    public required DateTimeOffset ObservedAtUtc { get; init; }

    /// <summary>Gets when Events received it (UTC).</summary>
    public required DateTimeOffset ReceivedAtUtc { get; init; }

    /// <summary>Gets the version of the mapping that produced the payload; none while SCFD does not send it.</summary>
    public string? MappingVersion { get; init; }

    /// <summary>Gets the source links behind it.</summary>
    public IReadOnlyList<ObservationLinkModel> Links { get; init; } = [];

    /// <summary>Gets what intake did with it.</summary>
    public required ObservationState State { get; init; }

    /// <summary>Gets why it was quarantined.</summary>
    public string? QuarantineReason { get; init; }

    /// <summary>Gets the event it created, changed or proposes to change.</summary>
    public Guid? EventId { get; init; }

    /// <summary>Gets the facts it proposes, read from its payload.</summary>
    public EventFacts? Proposed { get; init; }

    /// <summary>Gets the payload as delivered.</summary>
    public JsonElement? Payload { get; init; }
}
