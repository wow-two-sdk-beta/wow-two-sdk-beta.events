using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>Represents one actor's view of an Events implementation: every broker, each acting for that actor.</summary>
/// <param name="Catalogs">The catalogs broker.</param>
/// <param name="Events">The catalog events broker.</param>
/// <param name="Review">The review broker.</param>
/// <param name="Tracking">The tracking broker.</param>
/// <param name="Workflows">The workflows broker.</param>
/// <param name="Imports">The imports broker.</param>
/// <param name="Calendar">The calendar broker.</param>
/// <param name="Intake">The intake broker.</param>
public sealed record EventsBrokerSet(
    IEventCatalogsBroker Catalogs,
    ICatalogEventsBroker Events,
    IEventReviewBroker Review,
    IEventTrackingBroker Tracking,
    IEventWorkflowsBroker Workflows,
    IEventImportsBroker Imports,
    IEventCalendarBroker Calendar,
    IEventIntakeBroker Intake);
