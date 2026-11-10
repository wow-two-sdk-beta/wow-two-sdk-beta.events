using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Imports;

/// <summary>Holds one import row after validation, in the shape the import writes.</summary>
/// <param name="LegacyId">The row's id in the producing system.</param>
/// <param name="SourceId">The event's source identity, <c>{source}/{legacyId}</c>.</param>
/// <param name="Facts">The normalized facts.</param>
/// <param name="SourceUrl">The page the producing system found the event on.</param>
/// <param name="RecordedAtUtc">When the producing system first recorded the event.</param>
/// <param name="Tracking">The tracking to write, its assignee unset; none imports the event alone.</param>
/// <param name="Legacy">The producing system's row as JSON text, kept as evidence; none when absent.</param>
internal sealed record ImportRowInput(
    string LegacyId,
    string SourceId,
    EventFacts Facts,
    string? SourceUrl,
    DateTimeOffset? RecordedAtUtc,
    TrackingValues? Tracking,
    string? Legacy);
