using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Workflows;

/// <summary>Integrates the caller organization's workflow in process, keyed by its (issuer, organization).</summary>
/// <remarks>An organization that never wrote one reads <see cref="WorkflowStore.Default"/> at revision 0. A write to an
/// existing workflow names its revision; a missing or stale one fails with <see cref="EventsErrors.RevisionConflict"/>,
/// as does a first write that names a revision other than 0.</remarks>
/// <param name="db">The module's store.</param>
/// <param name="callers">Who calls.</param>
/// <param name="time">The clock rows are stamped with.</param>
internal sealed class ModuleWorkflowsBroker(EventsDbContext db, IEventsCallerService callers, TimeProvider time) : IEventWorkflowsBroker
{
    /// <inheritdoc />
    public async Task<Result<WorkflowModel>> GetAsync(CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        if (!caller.HasOrganization)
            return Result<WorkflowModel>.Fail(EventsErrors.OrganizationRequired());

        return Result<WorkflowModel>.Ok(await WorkflowStore.LoadAsync(db, caller, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<WorkflowModel>> PutAsync(WorkflowSpec spec, CancellationToken cancellationToken = default)
    {
        var caller = callers.Current;
        if (!caller.HasOrganization)
            return Result<WorkflowModel>.Fail(EventsErrors.OrganizationRequired());
        if (Validate(spec, out var stages) is { } invalid)
            return Result<WorkflowModel>.Fail(invalid);

        var row = await WorkflowStore.FindAsync(db, caller, cancellationToken);
        var fresh = row is null ? spec.ExpectedRevision is null or 0 : spec.ExpectedRevision == row.Revision;
        if (!fresh)
            return Result<WorkflowModel>.Fail(EventsErrors.RevisionConflict());

        var keys = stages.Select(stage => stage.Key).ToArray();
        var inUse = await db.Tracking.AsNoTracking()
            .Where(tracking => tracking.Issuer == caller.Issuer && tracking.OrganizationId == caller.OrganizationId && !keys.Contains(tracking.Stage))
            .Select(tracking => tracking.Stage)
            .Distinct()
            .OrderBy(stage => stage)
            .Take(10)
            .ToListAsync(cancellationToken);
        if (inUse.Count > 0)
            return Result<WorkflowModel>.Fail(EventsErrors.Invalid(
                "stages",
                $"'{string.Join("', '", inUse)}' cannot be removed: the organization's tracking still uses it"));

        if (row is null)
        {
            row = new WorkflowEntity { Issuer = caller.Issuer, OrganizationId = caller.OrganizationId!, Revision = 1 };
            db.Workflows.Add(row);
        }
        else
        {
            row.Revision++;
        }

        row.Stages = EventsJson.Write(stages);
        row.DefaultStage = spec.DefaultStage;
        row.Fields = EventsJson.Write(spec.Fields);
        row.UpdatedAt = time.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<WorkflowModel>.Fail(EventsErrors.RevisionConflict());
        }
        catch (DbUpdateException exception) when (StoreErrors.IsUniqueViolation(exception))
        {
            return Result<WorkflowModel>.Fail(EventsErrors.RevisionConflict());
        }

        return Result<WorkflowModel>.Ok(WorkflowStore.ToModel(row));
    }

    private static AppError? Validate(WorkflowSpec? spec, out List<WorkflowStageModel> stages)
    {
        stages = [];
        if (spec is null)
            return EventsErrors.Invalid("workflow", "required");

        var given = spec.Stages ?? [];
        if (given.Count is 0 or > EventsConstants.MaxListLength)
            return EventsErrors.Invalid("stages", $"1 to {EventsConstants.MaxListLength} stages");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < given.Count; index++)
        {
            var stage = given[index];
            if (stage?.Key is not { } key || !EventFactsValidator.KeyPattern().IsMatch(key))
                return EventsErrors.Invalid($"stages[{index}].key", EventsConstants.KeyPattern);
            if (!seen.Add(key))
                return EventsErrors.Invalid($"stages[{index}].key", $"'{key}' repeats an earlier stage");
            if (!Enum.IsDefined(stage.Class))
                return EventsErrors.Invalid($"stages[{index}].class", "open, active, won or lost");

            var label = (stage.Label ?? new Dictionary<string, string>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.Ordinal);
            if (label.Values.Any(text => text.Length > EventsConstants.MaxNameLength))
                return EventsErrors.Invalid($"stages[{index}].label", $"at most {EventsConstants.MaxNameLength} characters per locale");

            stages.Add(stage with { Label = label });
        }

        if (spec.DefaultStage is null || !seen.Contains(spec.DefaultStage))
            return EventsErrors.Invalid("defaultStage", "one of the stages");
        if (spec.Fields is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Object) })
            return EventsErrors.Invalid("fields", "a JSON Schema object");

        return null;
    }
}
