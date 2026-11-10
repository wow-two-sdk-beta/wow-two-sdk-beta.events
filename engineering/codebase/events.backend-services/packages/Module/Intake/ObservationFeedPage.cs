namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Represents one page a feed answered.</summary>
/// <param name="Records">The records, in log order.</param>
/// <param name="Next">The cursor to commit with the page; none keeps the one read from.</param>
/// <param name="HasMore">Whether more records wait beyond the page.</param>
internal sealed record ObservationFeedPage(IReadOnlyList<ScfdDeliveryRecord> Records, string? Next, bool HasMore);
