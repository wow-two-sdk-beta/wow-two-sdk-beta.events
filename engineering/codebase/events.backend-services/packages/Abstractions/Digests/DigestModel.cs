namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents a digest: a schedule that sends a catalog's chosen events to people of one organization.</summary>
public sealed record DigestModel
{
    /// <summary>Gets the digest's id, a UUID v7.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the slug of the catalog its events come from.</summary>
    public required string Catalog { get; init; }

    /// <summary>Gets the name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether the schedule runs.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Gets the channel it goes out through.</summary>
    public required string Channel { get; init; }

    /// <summary>Gets when it goes out.</summary>
    public required DigestSchedule Schedule { get; init; }

    /// <summary>Gets which events go in.</summary>
    public required DigestSelection Selection { get; init; }

    /// <summary>Gets who receives it.</summary>
    public required IReadOnlyList<DigestRecipient> Recipients { get; init; }

    /// <summary>Gets the language a recipient without their own reads.</summary>
    public required string Language { get; init; }

    /// <summary>Gets the most events one message lists.</summary>
    public required int ItemCap { get; init; }

    /// <summary>Gets the template of each event's link; none takes the host's.</summary>
    public string? LinkTemplate { get; init; }

    /// <summary>Gets the next scheduled occurrence (UTC); none while disabled.</summary>
    public DateTimeOffset? NextRunAtUtc { get; init; }

    /// <summary>Gets when the last run started (UTC); none before the first.</summary>
    public DateTimeOffset? LastRunAtUtc { get; init; }

    /// <summary>Gets the revision a write must name.</summary>
    public required int Revision { get; init; }

    /// <summary>Gets when it was created (UTC).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Gets when it last changed (UTC).</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
