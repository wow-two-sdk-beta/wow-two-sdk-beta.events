namespace WoW.Two.Sdk.Beta.Be.Events;

/// <summary>Holds the organization permissions a person needs beside the app's scope (events-integration #21, #23).</summary>
public static class EventsPermissionConstants
{
    /// <summary>Reads the organization's catalogs and tracking.</summary>
    public const string Read = "events:read";

    /// <summary>Writes the organization's tracking and workflow.</summary>
    public const string Track = "events:track";

    /// <summary>Reviews and curates the catalogs the organization owns.</summary>
    public const string Review = "events:review";

    /// <summary>Manages the organization's catalogs, imports and intake.</summary>
    public const string Admin = "events:admin";
}
