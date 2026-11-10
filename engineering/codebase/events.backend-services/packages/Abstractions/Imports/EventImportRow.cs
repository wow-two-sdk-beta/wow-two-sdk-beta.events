using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Imports;

/// <summary>Represents one event of an import bundle, with the importing organization's tracking of it.</summary>
public sealed record EventImportRow
{
    /// <summary>Gets the row's id in the producing system; it keys the id map, so a rerun finds the same event.</summary>
    public required string LegacyId { get; init; }

    /// <summary>Gets the event's facts; dates keep their precision and certainty as stored.</summary>
    public required EventFacts Facts { get; init; }

    /// <summary>Gets the page the producing system found the event on.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Gets when the producing system first recorded the event (UTC).</summary>
    public DateTimeOffset? RecordedAtUtc { get; init; }

    /// <summary>Gets the organization's tracking; none imports the event alone.</summary>
    public EventImportTracking? Tracking { get; init; }
}
