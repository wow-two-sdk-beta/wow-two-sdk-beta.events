namespace WoW.Two.Sdk.Beta.Be.Events.Client;

/// <summary>Holds the names the Events HTTP client registers under.</summary>
public static class EventsClientConstants
{
    /// <summary>Holds the named <see cref="HttpClient"/> every Events call goes through; its handlers, the token handler a
    /// consumer adds included, serve every broker.</summary>
    public const string HttpClientName = "events";
}
