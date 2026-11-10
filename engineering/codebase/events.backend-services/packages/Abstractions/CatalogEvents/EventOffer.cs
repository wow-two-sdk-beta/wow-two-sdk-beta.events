namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents a price or terms attached to an event; an unknown price stays null, never 0.</summary>
public sealed record EventOffer
{
    /// <summary>Gets what the offer is.</summary>
    public required OfferKind Kind { get; init; }

    /// <summary>Gets the offer's name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the lowest price.</summary>
    public decimal? MinPrice { get; init; }

    /// <summary>Gets the highest price.</summary>
    public decimal? MaxPrice { get; init; }

    /// <summary>Gets the ISO 4217 currency of the prices.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets whether the offer is free.</summary>
    public bool Free { get; init; }

    /// <summary>Gets the terms as written.</summary>
    public string? Terms { get; init; }

    /// <summary>Gets where to take the offer.</summary>
    public string? Url { get; init; }
}
