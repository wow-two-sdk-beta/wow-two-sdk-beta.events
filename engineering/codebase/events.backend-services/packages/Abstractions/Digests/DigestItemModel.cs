using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents one event a digest lists, in no language: what every channel renders from.</summary>
public sealed record DigestItemModel
{
    /// <summary>Gets the event's id.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the event's key, when it has one.</summary>
    public string? Key { get; init; }

    /// <summary>Gets the title per locale.</summary>
    public required IReadOnlyDictionary<string, string> Title { get; init; }

    /// <summary>Gets when it starts.</summary>
    public required EventWhen Starts { get; init; }

    /// <summary>Gets when it ends.</summary>
    public EventWhen? Ends { get; init; }

    /// <summary>Gets its city.</summary>
    public string? City { get; init; }

    /// <summary>Gets whether it still happens as listed.</summary>
    public EventStatus Status { get; init; } = EventStatus.Scheduled;

    /// <summary>Gets its nearest deadline that has not passed; none when it has no open deadline.</summary>
    public EventDeadline? NextDeadline { get; init; }

    /// <summary>Gets its link, from the digest's template; none without a template.</summary>
    public string? Url { get; init; }

    /// <summary>Gets whether it was first published since the previous run.</summary>
    public bool IsNew { get; init; }
}
