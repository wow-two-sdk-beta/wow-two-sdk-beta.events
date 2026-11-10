using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents one organization's private record about one event.</summary>
public sealed record TrackingModel
{
    /// <summary>Gets the tracking's id, a UUID v7.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the event it tracks.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the stage, a key of the organization's workflow.</summary>
    public required string Stage { get; init; }

    /// <summary>Gets the priority.</summary>
    public required TrackingPriority Priority { get; init; }

    /// <summary>Gets the assignee's subject at the organization's issuer.</summary>
    public string? Assignee { get; init; }

    /// <summary>Gets the notes, at most <see cref="EventsConstants.MaxNotesLength"/> characters.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Gets the workflow's custom fields as an object.</summary>
    public JsonElement? Fields { get; init; }

    /// <summary>Gets references to the product's own entities.</summary>
    public IReadOnlyList<TrackingReference> References { get; init; } = [];

    /// <summary>Gets the revision a write must name.</summary>
    public required int Revision { get; init; }

    /// <summary>Gets when it was created (UTC).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Gets when it last changed (UTC).</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
