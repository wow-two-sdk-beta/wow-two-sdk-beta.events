namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Holds one intake source: an SCFD subscription pulled into a catalog, with its token client and pace.</summary>
public sealed class EventsIntakeSourceOptions
{
    /// <summary>Gets or sets the source's name, unique among sources (<c>^[a-z0-9-]{1,64}$</c>); its cursor is kept under it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the slug of the catalog the source delivers into; the catalog must be private.</summary>
    public string Catalog { get; set; } = string.Empty;

    /// <summary>Gets or sets the SCFD instance's name, stamped on every observation.</summary>
    public string Instance { get; set; } = "scfd";

    /// <summary>Gets or sets the SCFD service's base URL.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the SCFD subscription to pull.</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>Gets or sets the SCFD pipeline the subscription follows, used when a delivery names none.</summary>
    public string? PipelineId { get; set; }

    /// <summary>Gets or sets whether the source is polled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the pause between polls.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the page size asked of SCFD, 1 to 500.</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>Gets or sets how many pages one poll reads at most while more wait.</summary>
    public int MaxPagesPerPoll { get; set; } = 20;

    /// <summary>Gets or sets the token endpoint of the source's client-credentials client; none sends no token.</summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>Gets or sets the client id Events presents to SCFD's issuer.</summary>
    public string ClientId { get; set; } = "events";

    /// <summary>Gets or sets the client secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets or sets the scope the token asks for.</summary>
    public string? Scope { get; set; } = "harvest.events-read";

    /// <summary>Gets or sets the resource the token is asked for, SCFD's resource URI at the issuer; empty asks for
    /// <see cref="BaseUrl"/> with its trailing slash. Identity issues a service token for exactly one resource.</summary>
    public string? Resource { get; set; }

    /// <summary>Gets the resource a token is asked for: <see cref="Resource"/>, else <see cref="BaseUrl"/> with its
    /// trailing slash; none without either.</summary>
    /// <returns>The resource URI.</returns>
    public string? TokenResource() =>
        !string.IsNullOrWhiteSpace(Resource) ? Resource.Trim()
        : string.IsNullOrWhiteSpace(BaseUrl) ? null
        : BaseUrl.Trim().TrimEnd('/') + "/";
}
