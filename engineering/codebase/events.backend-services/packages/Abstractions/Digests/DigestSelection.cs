using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Picks a digest's events from its catalog's published events, as of an occurrence's local day.</summary>
/// <remarks>
///   - an event goes in when it matches at least one rule — <see cref="NewSinceLastRun"/>, <see cref="StartingWithinDays"/>,
///     <see cref="DeadlineWithinDays"/> — and every filter (statuses, kinds, cities, tracked only); with no rule set, every
///     dated event that has not ended matches
///   - an event that ended before the day never goes out; month and year dates count by the whole span they cover
///   - order: start ascending, unknown last, then title; the digest's item cap keeps the first
/// </remarks>
public sealed record DigestSelection
{
    /// <summary>Gets whether events first published since the previous run go in; no event the previous run sent is new
    /// again. A first run counts from the digest's creation.</summary>
    public bool NewSinceLastRun { get; init; }

    /// <summary>Gets the days ahead an event may start in to go in, 0 for today only; none turns the rule off.</summary>
    public int? StartingWithinDays { get; init; }

    /// <summary>Gets the days ahead an open deadline may fall in to go in, 0 for today only; none turns the rule off.</summary>
    public int? DeadlineWithinDays { get; init; }

    /// <summary>Gets the statuses an event must have; empty takes every status.</summary>
    public IReadOnlyList<EventStatus> Statuses { get; init; } = [];

    /// <summary>Gets the kinds (the catalog's formats) an event must have; empty takes every kind.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];

    /// <summary>Gets the cities an event must be in, matched without case; empty takes every city and none.</summary>
    public IReadOnlyList<string> Cities { get; init; } = [];

    /// <summary>Gets whether only events the digest's organization tracks go in.</summary>
    public bool TrackedOnly { get; init; }
}
