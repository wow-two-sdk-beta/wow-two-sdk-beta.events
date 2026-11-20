namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>References an event's image: the URL it lives at, and the Files copy SCFD made of it when there is one.
/// Events itself copies no bytes (events-integration #31).</summary>
public sealed record EventImage
{
    /// <summary>Gets the image's URL. A sourced image keeps its source URL, the external link that credits the source.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the Files file id (a GUID in <c>D</c> form) of SCFD's copy of the image, when SCFD holds one; the
    /// same name as <c>media[].fileRef</c> on SCFD's <c>wow2.event.v1</c> payload. A consumer reads the copy through its
    /// own Files access and shows <see cref="Url"/> as the credit.</summary>
    public string? FileRef { get; init; }

    /// <summary>Gets the source link the image was found on.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Gets the SCFD capture the image came from.</summary>
    public string? CaptureId { get; init; }

    /// <summary>Gets the credit to show beside the image.</summary>
    public string? Attribution { get; init; }
}
