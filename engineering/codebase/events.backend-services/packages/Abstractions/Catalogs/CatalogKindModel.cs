namespace WoW.Two.Sdk.Beta.Be.Events.Catalogs;

/// <summary>Represents one kind of a catalog's taxonomy, such as <c>runway</c> or <c>trade-fair</c>.</summary>
public sealed record CatalogKindModel
{
    /// <summary>Gets the kind's key, matching <see cref="EventsConstants.KeyPattern"/>.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the kind's label per locale.</summary>
    public IReadOnlyDictionary<string, string> Label { get; init; } = new Dictionary<string, string>();
}
