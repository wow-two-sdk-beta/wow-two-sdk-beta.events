namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents a digest rendered now without sending: the events, the message per language and the recipients a
/// send now would reach, with the same texts it would send.</summary>
public sealed record DigestPreviewModel
{
    /// <summary>Gets the digest's id.</summary>
    public required Guid DigestId { get; init; }

    /// <summary>Gets the instant it was rendered as of (UTC).</summary>
    public required DateTimeOffset AsOfUtc { get; init; }

    /// <summary>Gets how many events matched the selection.</summary>
    public required int Matched { get; init; }

    /// <summary>Gets the events that would go out, after the item cap.</summary>
    public required IReadOnlyList<DigestItemModel> Items { get; init; }

    /// <summary>Gets the message per language.</summary>
    public required IReadOnlyList<DigestTextModel> Texts { get; init; }

    /// <summary>Gets the recipients it would reach, each <c>pending</c>.</summary>
    public required IReadOnlyList<DigestRunRecipientModel> Recipients { get; init; }

    /// <summary>Gets what a run could not do, such as a role no resolver could read.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}
