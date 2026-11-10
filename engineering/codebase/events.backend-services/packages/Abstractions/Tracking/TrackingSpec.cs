using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Describes a tracking write; a missing stage takes the workflow's default.</summary>
public sealed record TrackingSpec
{
    /// <summary>Gets the stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the priority.</summary>
    public TrackingPriority Priority { get; init; } = TrackingPriority.Normal;

    /// <summary>Gets the assignee's subject.</summary>
    public string? Assignee { get; init; }

    /// <summary>Gets the notes.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Gets the custom fields as an object.</summary>
    public JsonElement? Fields { get; init; }

    /// <summary>Gets references to the product's own entities.</summary>
    public IReadOnlyList<TrackingReference> References { get; init; } = [];

    /// <summary>Gets the revision the caller read; required to change existing tracking.</summary>
    public int? ExpectedRevision { get; init; }
}
