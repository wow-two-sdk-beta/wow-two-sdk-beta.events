using WoW.Two.Sdk.Beta.Be.Events.Workflows;

namespace Events.Tests.Integration.Workflows;

/// <summary>Holds Ocharo Desk's 8-stage workflow, the shape the real producer writes before it imports.</summary>
internal static class OcharoWorkflow
{
    /// <summary>Gets the stage keys in order.</summary>
    public static IReadOnlyList<string> Keys { get; } =
        ["candidate", "shortlisted", "applying", "applied", "confirmed", "attended", "skipped", "rejected"];

    /// <summary>Builds the workflow to write.</summary>
    /// <param name="expectedRevision">The revision read.</param>
    public static WorkflowSpec Spec(int? expectedRevision = null) => new()
    {
        Stages = Keys.Select(key => new WorkflowStageModel
        {
            Key = key,
            Label = new Dictionary<string, string> { ["en"] = char.ToUpperInvariant(key[0]) + key[1..] },
            Class = key switch
            {
                "applying" or "applied" or "confirmed" => StageClass.Active,
                "attended" => StageClass.Won,
                "skipped" or "rejected" => StageClass.Lost,
                _ => StageClass.Open,
            },
        }).ToList(),
        DefaultStage = "candidate",
        ExpectedRevision = expectedRevision,
    };
}
