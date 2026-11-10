namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Describes a merge: the event a candidate merges into, and the revisions read of both.</summary>
public sealed record MergeDecision
{
    /// <summary>Gets the event the candidate merges into.</summary>
    public required Guid IntoId { get; init; }

    /// <summary>Gets the candidate's revision the reviewer read.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the target's revision the reviewer read.</summary>
    public required int IntoExpectedRevision { get; init; }
}
