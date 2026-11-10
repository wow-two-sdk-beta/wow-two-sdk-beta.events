using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Events.Review;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Review;

/// <summary>Maps observation rows to <see cref="ObservationModel"/>: evidence and proposals read from their jsonb.</summary>
internal static class ObservationModels
{
    /// <summary>Maps a row.</summary>
    /// <param name="row">The observation row.</param>
    /// <param name="catalog">The catalog's slug.</param>
    /// <returns>The model.</returns>
    public static ObservationModel ToModel(ObservationEntity row, string catalog) => new()
    {
        Id = row.Id,
        Catalog = catalog,
        SourceKind = EventsLabels.Parse<EventSourceKind>(row.SourceKind),
        SourceInstance = row.SourceInstance,
        SubscriptionId = row.SubscriptionId,
        PipelineId = row.PipelineId,
        DeliveryEventId = row.DeliveryEventId,
        ItemId = row.ItemId,
        Type = row.Type,
        Reason = row.Reason,
        PostId = row.PostId,
        Revision = row.Revision,
        ObservedAtUtc = row.ObservedAt,
        ReceivedAtUtc = row.ReceivedAt,
        MappingVersion = row.MappingVersion,
        Links = EventsJson.Read<List<ObservationLinkModel>>(row.Links),
        State = EventsLabels.Parse<ObservationState>(row.State),
        QuarantineReason = row.QuarantineReason,
        EventId = row.EventId,
        Proposed = row.Proposed is null ? null : EventsJson.Read<EventFacts>(row.Proposed),
        Payload = EventsJson.Element(row.Payload),
    };
}
