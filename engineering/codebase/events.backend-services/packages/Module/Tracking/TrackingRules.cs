using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Validates a tracking write: one rule set for the tracking broker and imports.</summary>
internal static class TrackingRules
{
    private const int MaxReferenceLength = 200;
    private const int MaxAssigneeLength = 256;

    /// <summary>Validates the fields of a tracking write and returns them in the row's shape.</summary>
    /// <param name="prefix">The field path's prefix, such as <c>tracking.</c>; empty for the tracking broker.</param>
    /// <param name="workflow">The organization's workflow.</param>
    /// <param name="stage">The stage; none takes the workflow's default.</param>
    /// <param name="priority">The priority.</param>
    /// <param name="assignee">The assignee's subject.</param>
    /// <param name="notes">The notes.</param>
    /// <param name="fields">The custom fields; an object when present.</param>
    /// <param name="references">The references.</param>
    /// <param name="values">The validated values.</param>
    /// <returns>The first error, or none when valid.</returns>
    public static AppError? Validate(
        string prefix,
        WorkflowModel workflow,
        string? stage,
        TrackingPriority priority,
        string? assignee,
        string? notes,
        JsonElement? fields,
        IReadOnlyList<TrackingReference>? references,
        out TrackingValues values)
    {
        values = null!;
        var key = stage ?? workflow.DefaultStage;
        if (!workflow.Stages.Any(item => string.Equals(item.Key, key, StringComparison.Ordinal)))
            return prefix.Length == 0
                ? EventsErrors.StageUnknown(key)
                : EventsErrors.Invalid($"{prefix}stage", $"'{key}' is not a stage of the organization's workflow");

        if (!Enum.IsDefined(priority))
            return EventsErrors.Invalid($"{prefix}priority", "high, normal or low");

        var person = string.IsNullOrWhiteSpace(assignee) ? null : assignee.Trim();
        if (person is { Length: > MaxAssigneeLength })
            return EventsErrors.Invalid($"{prefix}assignee", $"at most {MaxAssigneeLength} characters");

        var text = notes ?? string.Empty;
        if (text.Length > EventsConstants.MaxNotesLength)
            return EventsErrors.Invalid($"{prefix}notes", $"at most {EventsConstants.MaxNotesLength} characters");

        if (fields is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Object) })
            return EventsErrors.Invalid($"{prefix}fields", "a JSON object");

        var refs = references ?? [];
        if (refs.Count > EventsConstants.MaxListLength)
            return EventsErrors.Invalid($"{prefix}references", $"at most {EventsConstants.MaxListLength}");

        var normalized = new List<TrackingReference>(refs.Count);
        for (var index = 0; index < refs.Count; index++)
        {
            var type = refs[index]?.Type?.Trim();
            var id = refs[index]?.Id?.Trim();
            if (string.IsNullOrEmpty(type) || type.Length > MaxReferenceLength || string.IsNullOrEmpty(id) || id.Length > MaxReferenceLength)
                return EventsErrors.Invalid($"{prefix}references[{index}]", $"a type and an id of 1 to {MaxReferenceLength} characters");
            normalized.Add(new TrackingReference(type, id));
        }

        values = new TrackingValues(key, EventsLabels.Of(priority), person, text, EventsJson.Write(fields), EventsJson.Write(normalized));
        return null;
    }
}
