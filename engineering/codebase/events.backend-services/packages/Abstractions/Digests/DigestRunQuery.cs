namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Pages a digest's runs, newest first.</summary>
public sealed record DigestRunQuery
{
    /// <summary>Gets the cursor a previous page returned.</summary>
    public string? Cursor { get; init; }

    /// <summary>Gets the page size, at most <see cref="EventsConstants.MaxPageSize"/>.</summary>
    public int Limit { get; init; } = 20;
}
