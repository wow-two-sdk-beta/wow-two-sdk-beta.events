using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;

namespace WoW.Two.Sdk.Beta.Be.Events.Imports;

/// <summary>Represents the importing organization's tracking of one imported event.</summary>
public sealed record EventImportTracking
{
    /// <summary>Gets the stage, a key of the organization's workflow.</summary>
    public required string Stage { get; init; }

    /// <summary>Gets the priority.</summary>
    public TrackingPriority Priority { get; init; } = TrackingPriority.Normal;

    /// <summary>Gets the notes.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Gets the custom fields as an object, such as <c>{ "participation": "…", "cost": "…" }</c>.</summary>
    public JsonElement? Fields { get; init; }

    /// <summary>Gets references to the product's own entities.</summary>
    public IReadOnlyList<TrackingReference> References { get; init; } = [];
}
