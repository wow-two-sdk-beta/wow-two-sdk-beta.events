namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Represents one recipient's message of a run, as a run hands it to its channel (<see cref="IDigestDelivery"/>).</summary>
public sealed record DigestMessage
{
    /// <summary>Gets the digest's id.</summary>
    public required Guid DigestId { get; init; }

    /// <summary>Gets the run's id.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Gets the catalog's slug.</summary>
    public required string Catalog { get; init; }

    /// <summary>Gets the issuer of the digest's organization; the recipient's <c>sub</c> is at this issuer.</summary>
    public required string Issuer { get; init; }

    /// <summary>Gets the digest's organization's id at its issuer.</summary>
    public required string OrganizationId { get; init; }

    /// <summary>Gets the person's <c>sub</c>.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the language it is rendered in.</summary>
    public required string Language { get; init; }

    /// <summary>Gets the text, in Telegram's HTML subset.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the events it lists, for a channel that renders its own text.</summary>
    public IReadOnlyList<DigestItemModel> Items { get; init; } = [];

    /// <summary>Gets the key a channel sends the message once per: the same for every retry of this recipient in this run.</summary>
    public required string IdempotencyKey { get; init; }
}
