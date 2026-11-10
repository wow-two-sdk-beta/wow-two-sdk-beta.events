namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says how a source reported an event.</summary>
public enum EventSourceKind
{
    /// <summary>An SCFD subscription's post.</summary>
    Scfd,

    /// <summary>An import bundle's row.</summary>
    Import,

    /// <summary>A curator's own entry.</summary>
    Manual,
}
