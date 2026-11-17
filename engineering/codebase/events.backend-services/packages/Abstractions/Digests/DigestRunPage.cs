namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents one page of a digest's runs, newest first.</summary>
/// <param name="Items">The runs on this page.</param>
/// <param name="NextCursor">The cursor of the next page; none on the last page.</param>
public sealed record DigestRunPage(IReadOnlyList<DigestRunModel> Items, string? NextCursor);
