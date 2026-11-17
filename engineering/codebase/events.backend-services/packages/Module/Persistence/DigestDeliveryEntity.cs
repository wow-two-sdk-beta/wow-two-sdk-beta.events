namespace WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

/// <summary>Maps a row of <c>events_digest_deliveries</c>: one recipient of a run and what became of their message.</summary>
internal sealed class DigestDeliveryEntity
{
    public Guid RunId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
