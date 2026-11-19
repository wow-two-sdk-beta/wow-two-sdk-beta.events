using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Asks for the organization's tracking summary: the day deadlines count from and the deadline kinds to list.</summary>
public sealed record TrackingSummaryQuery
{
    /// <summary>Gets the first day a deadline may fall on, in the caller's own calendar.</summary>
    public required DateOnly Today { get; init; }

    /// <summary>Gets the deadline kinds the nearest deadlines list; every kind when empty.</summary>
    public IReadOnlyList<DeadlineKind> DeadlineKinds { get; init; } = [];
}
