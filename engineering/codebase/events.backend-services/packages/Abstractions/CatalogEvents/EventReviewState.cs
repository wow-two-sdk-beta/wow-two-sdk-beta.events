namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says where an event stands in review; consumers see <see cref="Published"/> events only.</summary>
public enum EventReviewState
{
    /// <summary>Reported by a source and waiting for a reviewer.</summary>
    Candidate,

    /// <summary>Accepted by a reviewer or entered by a curator.</summary>
    Published,

    /// <summary>Rejected by a reviewer; its source identity stays, so a later revision does not resurrect it.</summary>
    Rejected,

    /// <summary>Removed from the catalog, or merged into another event.</summary>
    Archived,
}
