using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Reads one SCFD <c>DeliveryEvent</c> off the wire: Events' own copy until SCFD's client is on a registry
/// (events-integration #15). Every member is optional, so a malformed record is quarantined, never a failed page.</summary>
internal sealed record ScfdDeliveryRecord
{
    /// <summary>Gets the delivery's id, stable across retries.</summary>
    public Guid? EventId { get; init; }

    /// <summary>Gets the content identity across subscriptions and resyncs.</summary>
    public string? ItemId { get; init; }

    /// <summary>Gets the subscription whose log holds it.</summary>
    public Guid? SubscriptionId { get; init; }

    /// <summary>Gets the type, such as <c>post.new</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Gets when the change happened.</summary>
    public DateTimeOffset? OccurredAtUtc { get; init; }

    /// <summary>Gets the pipeline the subscription follows.</summary>
    public Guid? PipelineId { get; init; }

    /// <summary>Gets the post it is about.</summary>
    public Guid? PostId { get; init; }

    /// <summary>Gets the post's revision.</summary>
    public int? Revision { get; init; }

    /// <summary>Gets the link it is about.</summary>
    public Guid? LinkId { get; init; }

    /// <summary>Gets the run it is about.</summary>
    public Guid? RunId { get; init; }

    /// <summary>Gets why it was written: <c>ingest</c>, <c>backfill</c> or <c>resync</c>.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the subscription's context, echoed.</summary>
    public IReadOnlyDictionary<string, string>? Context { get; init; }

    /// <summary>Gets the payload, the <c>wow2.event.v1</c> projection for post records.</summary>
    public JsonElement? Data { get; init; }
}
