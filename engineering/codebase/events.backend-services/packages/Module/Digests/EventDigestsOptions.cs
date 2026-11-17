namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Holds the settings of the digests' scheduler and runs, bound from <see cref="SectionName"/>.</summary>
public sealed class EventDigestsOptions
{
    /// <summary>Holds the configuration section.</summary>
    public const string SectionName = "Events:Digests";

    /// <summary>Gets or sets whether the hosted scheduler runs due occurrences; a send now works either way.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets how often the scheduler looks for due occurrences and runs to resume.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how late an occurrence may still run after the host was down; an older one is recorded as
    /// missed and sends nothing.</summary>
    public TimeSpan CatchUpWindow { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Gets or sets how long a host holds a run while it sends; a run whose host died is resumed after it.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets how many sends a recipient's message gets when its channel fails in passing (a rate limit, a
    /// channel that is down) before it reads <c>failed</c>.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Gets the link template per catalog slug, for digests that name none: an absolute URL with <c>{id}</c>,
    /// <c>{key}</c> or <c>{catalog}</c>, such as the event page of the app that embeds the catalog; an empty value is none.</summary>
    public Dictionary<string, string> LinkTemplates { get; } = new(StringComparer.Ordinal);
}
