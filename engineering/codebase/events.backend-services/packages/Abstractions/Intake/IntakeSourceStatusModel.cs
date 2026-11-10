namespace WoW.Two.Sdk.Beta.Be.Events.Intake;

/// <summary>Represents one intake source's freshness: its cursor, its last poll and what waits in review.</summary>
public sealed record IntakeSourceStatusModel
{
    /// <summary>Gets the source's configured name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the catalog it delivers into.</summary>
    public required string Catalog { get; init; }

    /// <summary>Gets the SCFD subscription it pulls.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>Gets whether polling is on.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Gets the committed cursor; none before the first page.</summary>
    public string? Cursor { get; init; }

    /// <summary>Gets when the last poll finished (UTC).</summary>
    public DateTimeOffset? LastPolledAtUtc { get; init; }

    /// <summary>Gets when the last page with records committed (UTC).</summary>
    public DateTimeOffset? LastReceivedAtUtc { get; init; }

    /// <summary>Gets the newest observed time among received records (UTC); now minus it is the consumer's lag.</summary>
    public DateTimeOffset? NewestObservedAtUtc { get; init; }

    /// <summary>Gets the last poll's error, cleared by a successful poll.</summary>
    public string? LastError { get; init; }

    /// <summary>Gets how many polls failed in a row.</summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>Gets how many records it delivered.</summary>
    public long Received { get; init; }

    /// <summary>Gets how many of them are quarantined.</summary>
    public long Quarantined { get; init; }
}
