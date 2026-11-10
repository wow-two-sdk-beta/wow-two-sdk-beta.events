using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Integrates a catalog's events — list, read, curate and archive them — in process or over the service.</summary>
/// <remarks>Every call acts for the current principal. Reading needs a catalog the caller can see; anyone but the owner
/// organization reads published events only. Curating needs the owner organization; a curator's entry publishes.</remarks>
public interface ICatalogEventsBroker
{
    /// <summary>Lists a catalog's events.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="query">The filters and the page.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>One page of events; <see cref="EventsErrors.CatalogNotFound"/> when the caller cannot see the catalog.</returns>
    Task<Result<EventPage>> ListAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default);

    /// <summary>Gets an event.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The event.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The event; <see cref="EventsErrors.EventNotFound"/> when the caller cannot see it.</returns>
    Task<Result<EventModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a published event by hand.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="spec">The facts.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The event; <see cref="EventsErrors.NotCatalogOwner"/> for another organization;
    /// <see cref="EventsErrors.KeyTaken"/> for a key already used.</returns>
    Task<Result<EventModel>> CreateAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Proposes an event: a member of the owner organization enters it, and it waits for a reviewer as a
    /// candidate (nothing a member proposes publishes unreviewed).</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="spec">The facts; the expected revision is ignored.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The candidate; <see cref="EventsErrors.NotCatalogOwner"/> for another organization;
    /// <see cref="EventsErrors.KeyTaken"/> for a key already used.</returns>
    Task<Result<EventModel>> ProposeAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Replaces an event's facts.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The event.</param>
    /// <param name="spec">The facts and the revision read.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The event; <see cref="EventsErrors.RevisionConflict"/> when the revision is stale.</returns>
    Task<Result<EventModel>> UpdateAsync(string catalog, Guid id, EventSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Archives an event: it leaves every list, its tracking stays.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="id">The event.</param>
    /// <param name="expectedRevision">The revision read.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The archived event.</returns>
    Task<Result<EventModel>> ArchiveAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default);
}
