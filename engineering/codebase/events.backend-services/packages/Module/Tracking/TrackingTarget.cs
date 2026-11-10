using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Holds the caller, the catalog and the event one tracking call acts on.</summary>
/// <param name="Caller">Who calls.</param>
/// <param name="Catalog">The readable catalog.</param>
/// <param name="Event">The event, published or the owner's candidate.</param>
internal sealed record TrackingTarget(EventsCaller Caller, CatalogEntity Catalog, CatalogEventEntity Event);
