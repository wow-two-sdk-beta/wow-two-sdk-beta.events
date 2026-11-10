using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Creates, changes and maps tracking rows and their history.</summary>
internal static class TrackingRows
{
    /// <summary>Creates a tracking row at revision 1.</summary>
    public static TrackingEntity Create(Guid id, EventsCaller caller, Guid eventId, TrackingValues values, DateTimeOffset now)
    {
        var row = new TrackingEntity
        {
            Id = id,
            Issuer = caller.Issuer,
            OrganizationId = caller.OrganizationId!,
            EventId = eventId,
            Revision = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(row, values);
        return row;
    }

    /// <summary>Tells whether a write would change the row.</summary>
    public static bool Differs(TrackingEntity row, TrackingValues values) =>
        !string.Equals(row.Stage, values.Stage, StringComparison.Ordinal)
        || !string.Equals(row.Priority, values.Priority, StringComparison.Ordinal)
        || !string.Equals(row.Assignee, values.Assignee, StringComparison.Ordinal)
        || !string.Equals(row.Notes, values.Notes, StringComparison.Ordinal)
        || !JsonComparison.Same(row.Fields, values.Fields)
        || !JsonComparison.Same(row.Refs, values.Refs);

    /// <summary>Writes the values into the row; the caller bumps the revision.</summary>
    public static void Apply(TrackingEntity row, TrackingValues values)
    {
        row.Stage = values.Stage;
        row.Priority = values.Priority;
        row.Assignee = values.Assignee;
        row.Notes = values.Notes;
        row.Fields = values.Fields;
        row.Refs = values.Refs;
    }

    /// <summary>Creates a history entry for a stage change by the caller.</summary>
    public static TrackingHistoryEntity History(Guid trackingId, string? fromStage, string toStage, EventsCaller caller, DateTimeOffset at) => new()
    {
        Id = Guid.CreateVersion7(at),
        TrackingId = trackingId,
        FromStage = fromStage,
        ToStage = toStage,
        ActorIssuer = string.IsNullOrEmpty(caller.Issuer) ? null : caller.Issuer,
        ActorSubject = caller.Subject,
        At = at,
    };

    /// <summary>Maps a row to the contract.</summary>
    public static TrackingModel ToModel(TrackingEntity row) => new()
    {
        Id = row.Id,
        EventId = row.EventId,
        Stage = row.Stage,
        Priority = EventsLabels.Parse<TrackingPriority>(row.Priority),
        Assignee = row.Assignee,
        Notes = row.Notes,
        Fields = EventsJson.Element(row.Fields),
        References = EventsJson.Read<List<TrackingReference>>(row.Refs),
        Revision = row.Revision,
        CreatedAtUtc = row.CreatedAt,
        UpdatedAtUtc = row.UpdatedAt,
    };

    /// <summary>Maps a history row to the contract.</summary>
    public static TrackingHistoryEntryModel ToModel(TrackingHistoryEntity row) => new()
    {
        FromStage = row.FromStage,
        ToStage = row.ToStage,
        ActorIssuer = row.ActorIssuer,
        ActorSubject = row.ActorSubject,
        AtUtc = row.At,
    };
}
