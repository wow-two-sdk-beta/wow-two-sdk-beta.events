namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Represents the caller organization's tracking at a glance in one catalog.</summary>
public sealed record TrackingSummaryModel
{
    /// <summary>Gets the count per stage of the workflow, zeros included.</summary>
    public required IReadOnlyDictionary<string, int> ByStage { get; init; }

    /// <summary>Gets the nearest open deadlines on or after the day asked, soonest first, skipping lost stages.</summary>
    public required IReadOnlyList<UpcomingDeadlineModel> UpcomingDeadlines { get; init; }
}
