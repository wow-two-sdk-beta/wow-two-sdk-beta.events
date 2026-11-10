namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says whether an event still happens as listed.</summary>
public enum EventStatus
{
    /// <summary>Happens as listed.</summary>
    Scheduled,

    /// <summary>Put off with no new date yet.</summary>
    Postponed,

    /// <summary>Moved to new dates.</summary>
    Rescheduled,

    /// <summary>Will not happen.</summary>
    Cancelled,

    /// <summary>Moved online.</summary>
    MovedOnline,
}
