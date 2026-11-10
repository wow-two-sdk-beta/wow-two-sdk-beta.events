namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents who runs an event (P1 embeds the organizer in the event).</summary>
public sealed record EventOrganizer
{
    /// <summary>Gets the organizer's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the organizer's website.</summary>
    public string? Website { get; init; }
}
