using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Represents one source link behind an observation: where the record was read and what was captured.</summary>
public sealed record ObservationLinkModel
{
    /// <summary>Gets the original page.</summary>
    public string? Url { get; init; }

    /// <summary>Gets SCFD's source id.</summary>
    public string? SourceId { get; init; }

    /// <summary>Gets SCFD's capture of the page.</summary>
    public string? CaptureId { get; init; }

    /// <summary>Gets when the page was captured (UTC).</summary>
    public DateTimeOffset? CapturedAtUtc { get; init; }

    /// <summary>Gets the source's own fields as read.</summary>
    public JsonElement? SourceFields { get; init; }
}
