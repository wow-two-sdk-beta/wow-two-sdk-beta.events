namespace WoW.Two.Sdk.Beta.Be.Events.Intake;

/// <summary>Represents the intake's state: every source, and what waits for reviewers.</summary>
public sealed record IntakeStatusModel
{
    /// <summary>Gets every configured source.</summary>
    public required IReadOnlyList<IntakeSourceStatusModel> Sources { get; init; }

    /// <summary>Gets how many candidates wait for review, over every catalog.</summary>
    public required long Candidates { get; init; }

    /// <summary>Gets how many source changes wait for review.</summary>
    public required long PendingChanges { get; init; }

    /// <summary>Gets when the oldest unresolved review item arrived (UTC).</summary>
    public DateTimeOffset? OldestUnresolvedAtUtc { get; init; }
}
