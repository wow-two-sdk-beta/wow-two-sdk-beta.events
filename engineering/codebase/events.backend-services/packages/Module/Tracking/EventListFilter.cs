namespace WoW.Two.Sdk.Beta.Be.Events.Module.Tracking;

/// <summary>Holds the event filters a published-events list applies, read from an <c>EventQuery</c> or a
/// <c>TrackingQuery</c>; text is matched by the caller, since tracking matches its notes too.</summary>
/// <param name="From">The first day: an event matches when it has not ended before it.</param>
/// <param name="To">The last day: an event matches when it starts on or before it.</param>
/// <param name="Kind">The kind's key.</param>
/// <param name="Country">The ISO-2 country.</param>
/// <param name="City">The city, matched without case.</param>
/// <param name="Status">The status label.</param>
/// <param name="DeadlineBefore">The last day an open deadline may fall on.</param>
/// <param name="IncludeUndated">Whether events with no known start match.</param>
internal sealed record EventListFilter(
    DateOnly? From,
    DateOnly? To,
    string? Kind,
    string? Country,
    string? City,
    string? Status,
    DateOnly? DeadlineBefore,
    bool IncludeUndated);
