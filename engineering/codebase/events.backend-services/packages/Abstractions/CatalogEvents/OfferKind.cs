namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says what an offer sells or invites.</summary>
public enum OfferKind
{
    /// <summary>A ticket to attend.</summary>
    Ticket,

    /// <summary>Exhibitor terms: a booth, a stand, a slot on the runway.</summary>
    Exhibitor,

    /// <summary>Sponsorship.</summary>
    Sponsorship,

    /// <summary>A submission fee.</summary>
    Submission,

    /// <summary>Anything else.</summary>
    Other,
}
