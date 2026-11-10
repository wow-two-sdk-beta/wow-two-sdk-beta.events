using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Represents one entry of the review queue: a candidate, or a published event with source changes waiting.</summary>
public sealed record ReviewItemModel
{
    /// <summary>Gets the event as it stands.</summary>
    public required EventModel Event { get; init; }

    /// <summary>Gets the pending observations proposing changes to it, oldest first; a candidate's own observations are its
    /// evidence.</summary>
    public IReadOnlyList<ObservationModel> Observations { get; init; } = [];
}
