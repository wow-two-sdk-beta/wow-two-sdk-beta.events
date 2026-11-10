namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents where an event happens; every field is optional (P1 embeds the place in the event).</summary>
public sealed record EventPlace
{
    /// <summary>Gets the venue's name.</summary>
    public string? Venue { get; init; }

    /// <summary>Gets the street address.</summary>
    public string? Address { get; init; }

    /// <summary>Gets the city.</summary>
    public string? City { get; init; }

    /// <summary>Gets the region.</summary>
    public string? Region { get; init; }

    /// <summary>Gets the ISO 3166-1 alpha-2 country code, upper case.</summary>
    public string? Country { get; init; }

    /// <summary>Gets the latitude.</summary>
    public double? Latitude { get; init; }

    /// <summary>Gets the longitude.</summary>
    public double? Longitude { get; init; }
}
