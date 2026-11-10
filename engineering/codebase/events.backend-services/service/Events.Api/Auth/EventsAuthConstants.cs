namespace Events.Api.Auth;

/// <summary>Holds the names of the Events API's authentication and authorization.</summary>
public static class EventsAuthConstants
{
    /// <summary>Holds the policy scheme that picks the bearer token or the console's session for each request.</summary>
    public const string Scheme = "events";

    /// <summary>Holds the policy of a workflow write: the <c>events.track</c> scope, and a person's <c>events:admin</c>.</summary>
    public const string WorkflowWritePolicy = "events.workflow.write";

    /// <summary>Holds the path Identity's revocation signals arrive at, under each audience.</summary>
    public const string SignalPath = "/" + WoW.Two.Sdk.Beta.Be.Events.EventsRouteConstants.Signals;

    /// <summary>Holds the BFF's signal route under the console's session routes, the client's <c>SignalUrl</c> at Identity.</summary>
    public const string ConsoleSignalPath = "api/identity/signal";

    /// <summary>Holds the audience the token scheme is registered with; never accepted, since every trusted issuer the
    /// resolver returns names its own audiences.</summary>
    internal const string UnsetAudience = "urn:wow2:events:audience-per-issuer";
}
