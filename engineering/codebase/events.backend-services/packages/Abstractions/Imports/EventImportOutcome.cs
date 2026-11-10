namespace WoW.Two.Sdk.Beta.Be.Events.Imports;

/// <summary>Says what an import did, or would do, with one row.</summary>
public enum EventImportOutcome
{
    /// <summary>Created the event, and its tracking when the row carries one.</summary>
    Created,

    /// <summary>Changed the event or its tracking, found through the id map.</summary>
    Updated,

    /// <summary>Found the event through the id map with nothing to change.</summary>
    Unchanged,

    /// <summary>Failed; the report says why.</summary>
    Failed,
}
