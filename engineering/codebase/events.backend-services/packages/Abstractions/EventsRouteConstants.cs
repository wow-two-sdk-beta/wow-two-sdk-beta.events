namespace WoW.Two.Sdk.Beta.Be.Events;

/// <summary>Holds the HTTP routes of the Events contract, shared by the service and the client.</summary>
/// <remarks>Every answer is <c>{ "data": … }</c>; a failure is a problem body. Routes are relative to the service root.</remarks>
public static class EventsRouteConstants
{
    /// <summary>Holds the API root.</summary>
    public const string Root = "api/events/v1";

    /// <summary>Holds the catalogs collection; a catalog is <c>catalogs/{slug}</c>.</summary>
    public const string Catalogs = Root + "/catalogs";

    /// <summary>Holds a catalog's events, relative to the catalog.</summary>
    public const string Events = "events";

    /// <summary>Holds a catalog's review queue, relative to the catalog.</summary>
    public const string Review = "review";

    /// <summary>Holds a catalog's observations, relative to the catalog.</summary>
    public const string Observations = "observations";

    /// <summary>Holds a catalog's imports, relative to the catalog.</summary>
    public const string Imports = "imports";

    /// <summary>Holds a catalog's calendar export, relative to the catalog.</summary>
    public const string Calendar = "events.ics";

    /// <summary>Holds the caller organization's tracking of a catalog's events, relative to the catalog.</summary>
    public const string Tracking = "tracking";

    /// <summary>Holds the caller organization's workflow.</summary>
    public const string Workflow = Root + "/workflow";

    /// <summary>Holds the intake's status.</summary>
    public const string Intake = Root + "/intake";

    /// <summary>Holds the route Identity's revocation signals arrive at.</summary>
    public const string Signals = "signals/identity";
}
