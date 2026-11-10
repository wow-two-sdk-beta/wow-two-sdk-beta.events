using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Workflows;

/// <summary>Reads an organization's workflow, or the default one for an organization that never wrote its own.</summary>
internal static class WorkflowStore
{
    /// <summary>Gets the default workflow, at revision 0: it is read, never stored.</summary>
    public static WorkflowModel Default { get; } = new()
    {
        Stages =
        [
            Stage("interested", StageClass.Open, "Interested", "Интересно", "Qiziqarli"),
            Stage("planning", StageClass.Active, "Planning", "Планируем", "Rejalashtirilmoqda"),
            Stage("going", StageClass.Active, "Going", "Идём", "Boramiz"),
            Stage("attended", StageClass.Won, "Attended", "Посетили", "Qatnashdik"),
            Stage("skipped", StageClass.Lost, "Skipped", "Пропустили", "O'tkazib yuborildi"),
        ],
        DefaultStage = "interested",
        Revision = 0,
    };

    /// <summary>Finds the caller organization's stored workflow row, tracked for a write.</summary>
    public static Task<WorkflowEntity?> FindAsync(EventsDbContext db, EventsCaller caller, CancellationToken cancellationToken) =>
        db.Workflows.FirstOrDefaultAsync(row => row.Issuer == caller.Issuer && row.OrganizationId == caller.OrganizationId, cancellationToken);

    /// <summary>Reads the caller organization's workflow; the default when it never wrote one.</summary>
    public static async Task<WorkflowModel> LoadAsync(EventsDbContext db, EventsCaller caller, CancellationToken cancellationToken)
    {
        var row = await db.Workflows.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Issuer == caller.Issuer && item.OrganizationId == caller.OrganizationId, cancellationToken);
        return row is null ? Default : ToModel(row);
    }

    /// <summary>Maps a stored row to the contract.</summary>
    public static WorkflowModel ToModel(WorkflowEntity row) => new()
    {
        Stages = EventsJson.Read<List<WorkflowStageModel>>(row.Stages),
        DefaultStage = row.DefaultStage,
        Fields = EventsJson.Element(row.Fields),
        Revision = row.Revision,
    };

    private static WorkflowStageModel Stage(string key, StageClass stageClass, string en, string ru, string uz) => new()
    {
        Key = key,
        Class = stageClass,
        Label = new Dictionary<string, string>(StringComparer.Ordinal) { ["en"] = en, ["ru"] = ru, ["uz"] = uz },
    };
}
