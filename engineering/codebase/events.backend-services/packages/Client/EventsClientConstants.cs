namespace WoW.Two.Sdk.Beta.Be.Events.Client;

/// <summary>Holds the names the Events HTTP client registers under.</summary>
public static class EventsClientConstants
{
    /// <summary>Holds the named <see cref="HttpClient"/> every Events call goes through; its handlers, the token handler a
    /// consumer adds included, serve every broker.</summary>
    public const string HttpClientName = "events";

    /// <summary>Holds the token client that sends the service's own client-credentials token, with
    /// <see cref="EventsClientOptions.Identity"/> set; its pipeline ends in <see cref="HttpClientName"/>'s.</summary>
    public const string ServiceHttpClientName = "events-service";

    /// <summary>Holds the token client that exchanges the person's token on the call, with
    /// <see cref="EventsClientOptions.Identity"/> set; its pipeline ends in <see cref="HttpClientName"/>'s.</summary>
    public const string PersonHttpClientName = "events-person";
}
