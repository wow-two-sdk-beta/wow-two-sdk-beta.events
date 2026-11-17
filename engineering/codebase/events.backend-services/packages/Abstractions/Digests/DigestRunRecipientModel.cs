namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents one recipient of a run and what became of their message.</summary>
public sealed record DigestRunRecipientModel
{
    /// <summary>Gets the person's <c>sub</c>.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the language their message was rendered in.</summary>
    public required string Language { get; init; }

    /// <summary>Gets the result (<see cref="DigestRecipientStatusConstants"/>): <c>pending</c>, <c>sent</c>,
    /// <c>not_linked</c>, <c>muted</c> or <c>failed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Gets why it failed, such as the channel's error code; none otherwise.</summary>
    public string? Error { get; init; }

    /// <summary>Gets how many sends were tried.</summary>
    public required int Attempts { get; init; }

    /// <summary>Gets when the result was last written (UTC).</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
