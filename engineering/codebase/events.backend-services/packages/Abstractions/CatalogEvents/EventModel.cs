namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents an event in a catalog: its facts, where it stands in review and who reported it.</summary>
public sealed record EventModel
{
    /// <summary>Gets the event's id, a UUID v7.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the slug of the catalog that holds it.</summary>
    public required string Catalog { get; init; }

    /// <summary>Gets what is known about it.</summary>
    public required EventFacts Facts { get; init; }

    /// <summary>Gets where it stands in review.</summary>
    public required EventReviewState Review { get; init; }

    /// <summary>Gets the event an archived event was merged into.</summary>
    public Guid? MergedInto { get; init; }

    /// <summary>Gets every source identity that reported it.</summary>
    public IReadOnlyList<EventSourceModel> Sources { get; init; } = [];

    /// <summary>Gets how many source changes wait for a reviewer.</summary>
    public int PendingChanges { get; init; }

    /// <summary>Gets the revision a write must name.</summary>
    public required int Revision { get; init; }

    /// <summary>Gets when it was created (UTC).</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Gets when it last changed (UTC).</summary>
    public required DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>Gets when it was first published (UTC).</summary>
    public DateTimeOffset? PublishedAtUtc { get; init; }
}
