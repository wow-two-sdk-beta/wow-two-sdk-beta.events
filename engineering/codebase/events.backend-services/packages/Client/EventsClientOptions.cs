namespace WoW.Two.Sdk.Beta.Be.Events.Client;

/// <summary>Holds the settings of the Events HTTP client.</summary>
public sealed class EventsClientOptions
{
    /// <summary>Gets or sets the absolute root URL of the running Events service; with its trailing slash, it is also the
    /// audience Events accepts, so a token handler asks for it as the resource.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Returns the service's root URL with its trailing slash, as calls resolve against it.</summary>
    /// <param name="baseAddress">The configured root URL.</param>
    /// <returns>The root URL ending in <c>/</c>.</returns>
    public static string Root(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        var root = baseAddress.AbsoluteUri;
        return root.EndsWith('/') ? root : root + "/";
    }
}
