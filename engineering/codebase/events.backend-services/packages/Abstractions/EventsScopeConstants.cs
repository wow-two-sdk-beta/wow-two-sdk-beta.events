namespace WoW.Two.Sdk.Beta.Be.Events;

/// <summary>Holds the scopes a calling app asks of Events, each named <c>events.{action}</c> (events-integration #21).</summary>
public static class EventsScopeConstants
{
    /// <summary>Holds the resource name Identity registers Events under.</summary>
    public const string Resource = "events";

    /// <summary>Reads a catalog's events and the caller organization's tracking.</summary>
    public const string Read = "events.read";

    /// <summary>Writes the caller organization's tracking and workflow.</summary>
    public const string Track = "events.track";

    /// <summary>Reviews candidates and curates a catalog's events.</summary>
    public const string Review = "events.review";

    /// <summary>Imports a bundle into a catalog and the caller organization's tracking.</summary>
    public const string Import = "events.import";

    /// <summary>Manages catalogs and reads the intake's status.</summary>
    public const string Admin = "events.admin";
}
