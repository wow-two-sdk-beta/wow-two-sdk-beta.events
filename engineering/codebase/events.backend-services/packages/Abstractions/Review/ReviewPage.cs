namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Represents one page of the review queue.</summary>
/// <param name="Items">The entries on this page.</param>
/// <param name="NextCursor">The cursor of the next page; none on the last page.</param>
public sealed record ReviewPage(IReadOnlyList<ReviewItemModel> Items, string? NextCursor);
