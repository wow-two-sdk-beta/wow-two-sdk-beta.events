using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Workflows;

/// <summary>Represents the stages and custom fields one organization tracks events with.</summary>
public sealed record WorkflowModel
{
    /// <summary>Gets the stages, in order.</summary>
    public required IReadOnlyList<WorkflowStageModel> Stages { get; init; }

    /// <summary>Gets the stage new tracking starts in.</summary>
    public required string DefaultStage { get; init; }

    /// <summary>Gets the custom fields' JSON Schema; stored, not enforced in P1.</summary>
    public JsonElement? Fields { get; init; }

    /// <summary>Gets the revision a write must name; 0 for the default workflow never written.</summary>
    public required int Revision { get; init; }
}
