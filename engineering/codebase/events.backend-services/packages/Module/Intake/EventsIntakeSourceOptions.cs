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

    /// <summary>Holds the review policy that waits for a person.</summary>
    public const string ManualReview = "manual";

    /// <summary>Holds the review policy that publishes what the classifier marks relevant.</summary>
    public const string AutoReview = "auto";

    /// <summary>Holds the classifier that trusts SCFD's subscription filter.</summary>
    public const string FilterClassifier = "filter";

    /// <summary>Holds the classifier that reads the record's own <c>classification</c>.</summary>
    public const string PayloadClassifier = "payload";

    /// <summary>Gets or sets the review policy: <see cref="ManualReview"/> (the default) keeps every new record a
    /// candidate; <see cref="AutoReview"/> publishes what the classifier marks relevant and holds the rest as candidates,
    /// recording the classifier and its verdict as the review's actor and reason (events-integration #53).</summary>
    public string Review { get; set; } = ManualReview;

    /// <summary>Gets or sets the classifier an automatic review reads: <see cref="FilterClassifier"/> (the default) or
    /// <see cref="PayloadClassifier"/>.</summary>
    public string Classifier { get; set; } = FilterClassifier;

    /// <summary>Gets or sets the name of the SCFD filter the subscription applies, such as <c>ocharo-fashion-v1</c>; it
    /// names the <see cref="FilterClassifier"/>.</summary>
    public string? Filter { get; set; }

    /// <summary>Gets or sets the lowest confidence a <see cref="PayloadClassifier"/> verdict needs to publish, 0 to 1.</summary>
    public double MinConfidence { get; set; }

    /// <summary>Gets whether the source reviews automatically.</summary>
    public bool ReviewsAutomatically => string.Equals(Review, AutoReview, StringComparison.OrdinalIgnoreCase);

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
