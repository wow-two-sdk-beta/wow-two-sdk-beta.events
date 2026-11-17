namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents one run of a digest: one occurrence of its schedule, or one send now, and what each recipient got.</summary>
public sealed record DigestRunModel
{
    /// <summary>Gets the run's id, a UUID v7; each recipient's message carries it in its idempotency key.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the digest's id.</summary>
    public required Guid DigestId { get; init; }

    /// <summary>Gets the occurrence it ran for (UTC); a send now's own instant. A digest runs each occurrence once.</summary>
    public required DateTimeOffset OccurrenceAtUtc { get; init; }

    /// <summary>Gets what started it.</summary>
    public required DigestRunTrigger Trigger { get; init; }

    /// <summary>Gets where it stands.</summary>
    public required DigestRunStatus Status { get; init; }

    /// <summary>Gets how many events matched the selection.</summary>
    public required int Matched { get; init; }

    /// <summary>Gets how many went out, after the item cap.</summary>
    public required int Selected { get; init; }

    /// <summary>Gets the events that went out, in their order.</summary>
    public IReadOnlyList<Guid> EventIds { get; init; } = [];

    /// <summary>Gets the message per language.</summary>
    public IReadOnlyList<DigestTextModel> Texts { get; init; } = [];

    /// <summary>Gets each recipient's result.</summary>
    public IReadOnlyList<DigestRunRecipientModel> Recipients { get; init; } = [];

    /// <summary>Gets what the run could not do, such as a role no resolver could read.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>Gets when it started (UTC).</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets when every recipient had a final result (UTC); none while running.</summary>
    public DateTimeOffset? FinishedAtUtc { get; init; }
}
