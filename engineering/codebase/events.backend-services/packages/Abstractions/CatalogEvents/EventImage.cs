namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>References an event's image where it already lives; Events copies no bytes in P1 (events-integration #31).</summary>
public sealed record EventImage
{
    /// <summary>Gets the image's URL.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the source link the image was found on.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Gets the SCFD capture the image came from.</summary>
    public string? CaptureId { get; init; }

    /// <summary>Gets the credit to show beside the image.</summary>
    public string? Attribution { get; init; }
}
