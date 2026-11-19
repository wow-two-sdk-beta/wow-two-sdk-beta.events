using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Module.Workflows;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Carries every organization's tracking of a duplicate's event to the event of the post SCFD merged it into, so
/// Ocharo stops listing one event twice (events-integration E18).</summary>
/// <remarks>
///   - an organization with no tracking on the survivor's event gets a copy: stage, priority, assignee, notes, fields and
///     references, a first history entry naming the merge
///   - an organization tracking both keeps the survivor's row, at the later of the two stages by its own workflow's order
///     (a stage the workflow lacks ranks before every stage it has; a tie keeps the survivor's), with the duplicate's notes
///     appended; a history entry names the merge whenever the row changed
///   - the duplicate's own tracking stays where it is, so SCFD's unmerge restores the duplicate's event whole; the carried
///     tracking stays on the survivor
/// </remarks>
internal static class MergedTracking
{
    private const string Separator = "\n\n";

    /// <summary>Holds the history actor's longest subject, the column's width.</summary>
    private const int MaxActorLength = 256;

    /// <summary>Names the history actor of a carried tracking: the source and the merged posts.</summary>
    /// <param name="instance">The SCFD instance.</param>
    /// <param name="duplicatePost">The merged post.</param>
    /// <param name="survivorPost">The post it merged into.</param>
    /// <returns>The actor's subject, cut to the column's width.</returns>
    public static string Actor(string instance, Guid duplicatePost, Guid survivorPost)
    {
        var actor = $"{IntakeProcessor.SourceActor}{instance} merged post {duplicatePost:D} into {survivorPost:D}";
        return actor.Length > MaxActorLength ? actor[..MaxActorLength] : actor;
    }

    /// <summary>Carries the duplicate event's tracking to the survivor's event and saves it, history included.</summary>
    /// <param name="db">The module's store, inside the page's transaction.</param>
    /// <param name="duplicateEventId">The duplicate's event.</param>
    /// <param name="survivorEventId">The survivor's event.</param>
    /// <param name="actor">The history actor's subject, from <see cref="Actor"/>.</param>
    /// <param name="now">The time the change is stamped with.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>A task that completes when the tracking and its history are saved.</returns>
    public static async Task CarryAsync(
        EventsDbContext db, Guid duplicateEventId, Guid survivorEventId, string actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var duplicates = await db.Tracking.AsNoTracking().Where(row => row.EventId == duplicateEventId).ToListAsync(cancellationToken);
        if (duplicates.Count == 0)
            return;

        var survivors = await db.Tracking.Where(row => row.EventId == survivorEventId).ToListAsync(cancellationToken);
        var history = new List<TrackingHistoryEntity>();
        foreach (var duplicate in duplicates)
        {
            var kept = survivors.FirstOrDefault(row => row.Issuer == duplicate.Issuer && row.OrganizationId == duplicate.OrganizationId);
            if (kept is null)
            {
                var copy = new TrackingEntity
                {
                    Id = Guid.CreateVersion7(now),
                    Issuer = duplicate.Issuer,
                    OrganizationId = duplicate.OrganizationId,
                    EventId = survivorEventId,
                    Stage = duplicate.Stage,
                    Priority = duplicate.Priority,
                    Assignee = duplicate.Assignee,
                    Notes = duplicate.Notes,
                    Fields = duplicate.Fields,
                    Refs = duplicate.Refs,
                    Revision = 1,
                    CreatedAt = duplicate.CreatedAt,
                    UpdatedAt = now,
                };
                db.Tracking.Add(copy);
                history.Add(TrackingRows.HistoryBy(copy.Id, null, copy.Stage, actor, now));
                continue;
            }

            var before = kept.Stage;
            var later = false;
            if (!string.Equals(duplicate.Stage, kept.Stage, StringComparison.Ordinal))
            {
                var workflow = await WorkflowStore.LoadAsync(db, duplicate.Issuer, duplicate.OrganizationId, cancellationToken);
                later = Rank(workflow, duplicate.Stage) > Rank(workflow, kept.Stage);
            }

            var notes = Append(kept.Notes, duplicate.Notes);
            if (!later && notes == kept.Notes)
                continue;

            if (later)
                kept.Stage = duplicate.Stage;
            kept.Notes = notes;
            kept.Revision++;
            kept.UpdatedAt = now;
            history.Add(TrackingRows.HistoryBy(kept.Id, before, kept.Stage, actor, now));
        }

        // The model declares no relationships, so a history row goes in a second save, after the tracking it references.
        await db.SaveChangesAsync(cancellationToken);
        if (history.Count == 0)
            return;

        db.TrackingHistory.AddRange(history);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Gets a stage's place in the workflow's order; before every stage when the workflow lacks it.</summary>
    private static int Rank(WorkflowModel workflow, string stage)
    {
        for (var index = 0; index < workflow.Stages.Count; index++)
        {
            if (string.Equals(workflow.Stages[index].Key, stage, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    /// <summary>Appends the duplicate's notes after the survivor's, cut to the notes' limit; nothing when they hold none or
    /// the survivor's already contain them.</summary>
    private static string Append(string survivor, string duplicate)
    {
        if (string.IsNullOrWhiteSpace(duplicate) || survivor.Contains(duplicate, StringComparison.Ordinal))
            return survivor;

        var joined = survivor.Length == 0 ? duplicate : survivor + Separator + duplicate;
        return joined.Length > EventsConstants.MaxNotesLength ? joined[..EventsConstants.MaxNotesLength] : joined;
    }
}
