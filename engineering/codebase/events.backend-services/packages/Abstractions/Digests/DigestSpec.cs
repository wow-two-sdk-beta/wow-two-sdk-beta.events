namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Describes a digest to create or replace: what goes out of a catalog, when, to whom and through which channel.</summary>
public sealed record DigestSpec
{
    /// <summary>Gets the name, 1 to <see cref="EventsConstants.MaxNameLength"/> characters; it heads every message.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether the schedule runs; a disabled digest still previews and sends on demand.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the channel it goes out through (<see cref="DigestChannelConstants"/>); the host must register it.</summary>
    public string Channel { get; init; } = DigestChannelConstants.Telegram;

    /// <summary>Gets when it goes out.</summary>
    public required DigestSchedule Schedule { get; init; }

    /// <summary>Gets which events go in.</summary>
    public DigestSelection Selection { get; init; } = new();

    /// <summary>Gets who receives it; 1 to <see cref="EventsConstants.MaxDigestRecipients"/> entries.</summary>
    public IReadOnlyList<DigestRecipient> Recipients { get; init; } = [];

    /// <summary>Gets the language a recipient without their own reads (<see cref="DigestLanguageConstants"/>).</summary>
    public string Language { get; init; } = DigestLanguageConstants.UzbekLatin;

    /// <summary>Gets the most events one message lists, 1 to <see cref="EventsConstants.MaxDigestItems"/>.</summary>
    public int ItemCap { get; init; } = 20;

    /// <summary>Gets the absolute <c>https</c> or <c>http</c> template of each event's link, with <c>{id}</c>, <c>{key}</c>
    /// (the key, or the id when none) and <c>{catalog}</c>, such as <c>https://app.example.com/events/{id}</c>; none takes
    /// the host's template for the catalog, and with neither the events go out without links.</summary>
    public string? LinkTemplate { get; init; }

    /// <summary>Gets the revision the caller read; none when creating.</summary>
    public int? ExpectedRevision { get; init; }
}
