namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says what a deadline closes.</summary>
public enum DeadlineKind
{
    /// <summary>Applications to take part: exhibit, show, compete.</summary>
    Application,

    /// <summary>Registration to attend.</summary>
    Registration,

    /// <summary>An early-bird price.</summary>
    EarlyBird,

    /// <summary>A submission: a collection, a work, a paper.</summary>
    Submission,

    /// <summary>A payment.</summary>
    Payment,

    /// <summary>Anything else; the label says what.</summary>
    Other,
}
