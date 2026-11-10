namespace Events.Api.Auth;

/// <summary>Holds who may call the Events API with a token: the trusted issuers and the audiences their tokens may name.</summary>
/// <remarks>Bound from <c>Events:Auth</c> and read on use, so a test host's late configuration reaches it.</remarks>
public sealed class EventsAuthSettings
{
    /// <summary>Holds the section the settings bind from.</summary>
    public const string SectionName = "Events:Auth";

    /// <summary>Gets or sets this instance's own resource URI, with its trailing slash: the Events <c>app</c> site's URL,
    /// the audience of every issuer that names none.</summary>
    public string? Audience { get; set; }

    /// <summary>Gets the issuers whose tokens the API accepts.</summary>
    public List<EventsIssuerSettings> Issuers { get; } = [];
}
