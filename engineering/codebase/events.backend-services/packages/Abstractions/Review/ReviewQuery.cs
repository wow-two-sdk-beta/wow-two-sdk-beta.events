namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Pages a catalog's review queue: candidates first, then published events with pending changes, oldest first.</summary>
public sealed record ReviewQuery
{
    /// <summary>Gets the cursor a previous page returned.</summary>
    public string? Cursor { get; init; }

    /// <summary>Gets the page size, at most <see cref="EventsConstants.MaxPageSize"/>.</summary>
    public int Limit { get; init; } = EventsConstants.DefaultPageSize;
}
