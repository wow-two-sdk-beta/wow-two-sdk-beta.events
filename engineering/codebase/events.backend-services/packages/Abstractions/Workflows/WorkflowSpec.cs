using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Workflows;

/// <summary>Describes an organization's workflow to write.</summary>
public sealed record WorkflowSpec
{
    /// <summary>Gets the stages, in order; 1 to <see cref="EventsConstants.MaxListLength"/>, keys unique.</summary>
    public required IReadOnlyList<WorkflowStageModel> Stages { get; init; }

    /// <summary>Gets the stage new tracking starts in; one of the stages.</summary>
    public required string DefaultStage { get; init; }

    /// <summary>Gets the custom fields' JSON Schema.</summary>
    public JsonElement? Fields { get; init; }

    /// <summary>Gets the revision the caller read.</summary>
    public int? ExpectedRevision { get; init; }
}
