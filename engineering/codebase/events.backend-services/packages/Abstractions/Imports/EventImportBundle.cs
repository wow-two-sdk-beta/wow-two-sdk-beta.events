namespace WoW.Two.Sdk.Beta.Be.Events.Imports;

/// <summary>Represents a <c>wow2.events-import.v1</c> bundle: a product's own events moving into a catalog.</summary>
/// <remarks>Imported events publish: the producing organization's people entered them. The whole bundle validates
/// before any write.</remarks>
public sealed record EventImportBundle
{
    /// <summary>Gets the format; must be <see cref="EventsConstants.ImportFormat"/>.</summary>
    public string Format { get; init; } = EventsConstants.ImportFormat;

    /// <summary>Gets the producing system's name, such as <c>ocharo-desk</c>; it prefixes each row's source identity.</summary>
    public required string Source { get; init; }

    /// <summary>Gets the rows, 1 to <see cref="EventsConstants.MaxImportRows"/>, legacy ids unique.</summary>
    public required IReadOnlyList<EventImportRow> Rows { get; init; }
}
