namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Represents one page of observations.</summary>
/// <param name="Items">The observations on this page, newest first.</param>
/// <param name="NextCursor">The cursor of the next page; none on the last page.</param>
public sealed record ObservationPage(IReadOnlyList<ObservationModel> Items, string? NextCursor);
