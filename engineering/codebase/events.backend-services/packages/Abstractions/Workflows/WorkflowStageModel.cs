namespace WoW.Two.Sdk.Beta.Be.Events.Workflows;

/// <summary>Represents one stage of an organization's workflow.</summary>
public sealed record WorkflowStageModel
{
    /// <summary>Gets the stage's key, matching <see cref="EventsConstants.KeyPattern"/>.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the stage's label per locale.</summary>
    public IReadOnlyDictionary<string, string> Label { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets what the stage means.</summary>
    public StageClass Class { get; init; } = StageClass.Open;
}
