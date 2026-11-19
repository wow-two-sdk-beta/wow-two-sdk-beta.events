namespace WoW.Two.Sdk.Beta.Be.Events.Intake;

/// <summary>Represents how fresh a catalog's intake is, for the catalog's members: times and a count only, never a cursor,
/// an error text, an address or a token.</summary>
public sealed record IntakeFreshnessModel
{
    /// <summary>Gets when a source of the catalog last committed a page with records (UTC); none before the first.</summary>
    public DateTimeOffset? LastReceivedAtUtc { get; init; }

    /// <summary>Gets the newest observed time among the records the catalog's sources received (UTC); now minus it is the
    /// consumer's lag, and none before the first record.</summary>
    public DateTimeOffset? NewestObservedAtUtc { get; init; }

    /// <summary>Gets how many of the catalog's polling sources failed their last poll; zero with no source.</summary>
    public required int FailingSources { get; init; }
}
