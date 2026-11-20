namespace WoW.Two.Sdk.Beta.Be.Events.Tracking;

/// <summary>Says how a tracking list is ordered; every order ends on the event's id, so pages never repeat or skip an
/// event that shares its position with another.</summary>
public enum TrackingSort
{
    /// <summary>Start ascending with unknown last, then title: the default, which month groups read well under.</summary>
    Starts,

    /// <summary>Newest published first.</summary>
    Added,

    /// <summary>Nearest open deadline first, then title; events with no open deadline come last.</summary>
    Deadline,
}
