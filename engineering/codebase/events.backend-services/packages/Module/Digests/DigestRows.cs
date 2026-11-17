using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Maps digest, run and delivery rows to the contract's models, and a spec onto a row.</summary>
internal static class DigestRows
{
    /// <summary>Holds the run statuses a later run counts from: the runs that sent, or found nothing to send.</summary>
    public static readonly string[] CountedStatuses =
    [
        EventsLabels.Of(DigestRunStatus.Completed), EventsLabels.Of(DigestRunStatus.Partial), EventsLabels.Of(DigestRunStatus.Empty),
    ];

    /// <summary>Holds the label of a running run.</summary>
    public static readonly string Running = EventsLabels.Of(DigestRunStatus.Running);

    /// <summary>Writes a normalized spec onto a row.</summary>
    public static void Apply(DigestEntity row, DigestSpec spec, DateTimeOffset now)
    {
        row.Name = spec.Name;
        row.Enabled = spec.Enabled;
        row.Channel = spec.Channel;
        row.TimeZone = spec.Schedule.TimeZone;
        row.Schedule = EventsJson.Write(spec.Schedule);
        row.Selection = EventsJson.Write(spec.Selection);
        row.Recipients = EventsJson.Write(spec.Recipients);
        row.Language = spec.Language;
        row.ItemCap = spec.ItemCap;
        row.LinkTemplate = spec.LinkTemplate;
        row.UpdatedAt = now;
    }

    /// <summary>Reads a row's schedule.</summary>
    public static DigestSchedule Schedule(DigestEntity row) => EventsJson.Read<DigestSchedule>(row.Schedule);

    /// <summary>Reads a row's selection.</summary>
    public static DigestSelection Selection(DigestEntity row) => EventsJson.Read<DigestSelection>(row.Selection);

    /// <summary>Reads a row's recipients.</summary>
    public static List<DigestRecipient> Recipients(DigestEntity row) => EventsJson.Read<List<DigestRecipient>>(row.Recipients);

    /// <summary>Computes the next occurrence a row's schedule runs at after an instant; none while disabled.</summary>
    public static DateTimeOffset? NextRunAt(DigestEntity row, DateTimeOffset after)
    {
        if (!row.Enabled || !DigestOccurrences.TryFindZone(row.TimeZone, out var zone))
            return null;

        var from = after > row.ScheduleSince ? after : row.ScheduleSince;
        return DigestOccurrences.Next(Schedule(row), zone, from) is { } next ? StoredClock.Truncate(next) : null;
    }

    /// <summary>Maps a row.</summary>
    public static DigestModel ToModel(DigestEntity row, string catalog, DateTimeOffset? lastRunAt) => new()
    {
        Id = row.Id,
        Catalog = catalog,
        Name = row.Name,
        Enabled = row.Enabled,
        Channel = row.Channel,
        Schedule = Schedule(row),
        Selection = Selection(row),
        Recipients = Recipients(row),
        Language = row.Language,
        ItemCap = row.ItemCap,
        LinkTemplate = row.LinkTemplate,
        NextRunAtUtc = row.Enabled ? row.NextRunAt : null,
        LastRunAtUtc = lastRunAt,
        Revision = row.Revision,
        CreatedAtUtc = row.CreatedAt,
        UpdatedAtUtc = row.UpdatedAt,
    };

    /// <summary>Maps a run with its deliveries, in their order.</summary>
    public static DigestRunModel ToModel(DigestRunEntity run, IEnumerable<DigestDeliveryEntity> deliveries) => new()
    {
        Id = run.Id,
        DigestId = run.DigestId,
        OccurrenceAtUtc = run.OccurrenceAt,
        Trigger = EventsLabels.Parse<DigestRunTrigger>(run.Trigger),
        Status = EventsLabels.Parse<DigestRunStatus>(run.Status),
        Matched = run.Matched,
        Selected = run.Selected,
        EventIds = EventsJson.Read<List<Guid>>(run.EventIds),
        Texts = EventsJson.Read<List<DigestTextModel>>(run.Texts),
        Recipients = deliveries.OrderBy(delivery => delivery.Ordinal).Select(ToModel).ToList(),
        Notes = EventsJson.Read<List<string>>(run.Notes),
        StartedAtUtc = run.StartedAt,
        FinishedAtUtc = run.FinishedAt,
    };

    /// <summary>Maps a delivery.</summary>
    public static DigestRunRecipientModel ToModel(DigestDeliveryEntity delivery) => new()
    {
        Subject = delivery.Subject,
        Language = delivery.Language,
        Status = delivery.Status,
        Error = delivery.Error,
        Attempts = delivery.Attempts,
        UpdatedAtUtc = delivery.UpdatedAt,
    };
}
