namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says where a source's listing of an event stands, as the source last reported it.</summary>
public enum EventSourceState
{
    /// <summary>The listing is up.</summary>
    Live,

    /// <summary>The source could not confirm the listing on its last check; it may be going away.</summary>
    Suspect,

    /// <summary>The listing is gone from the source.</summary>
    Gone,

    /// <summary>The listing is up, but its post no longer passes the subscription's filter: the source no longer offers it
    /// as this catalog's kind of event.</summary>
    Unmatched,
}
