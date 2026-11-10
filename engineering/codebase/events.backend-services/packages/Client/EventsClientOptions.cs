namespace WoW.Two.Sdk.Beta.Be.Events.Client;

/// <summary>Holds the settings of the Events HTTP client.</summary>
public sealed class EventsClientOptions
{
    /// <summary>Gets or sets the absolute root URL of the running Events service.</summary>
    public Uri? BaseAddress { get; set; }
}
