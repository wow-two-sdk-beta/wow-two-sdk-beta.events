namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Says what intake did with an observation.</summary>
public enum ObservationState
{
    /// <summary>Created or updated a candidate event.</summary>
    Applied,

    /// <summary>Proposes a change to a published event and waits for a reviewer.</summary>
    Pending,

    /// <summary>Kept as evidence only: a newer revision of the same post arrived first, or a later one replaced it.</summary>
    Superseded,

    /// <summary>Malformed: stored with its reason, never applied, never blocking the page.</summary>
    Quarantined,

    /// <summary>A lifecycle or run record: it changes evidence, never a fact.</summary>
    Recorded,

    /// <summary>A pending change a reviewer applied to the event.</summary>
    Accepted,

    /// <summary>A pending change a reviewer dismissed.</summary>
    Dismissed,

    /// <summary>Reported a post of a rejected event; kept as evidence only.</summary>
    Ignored,
}
