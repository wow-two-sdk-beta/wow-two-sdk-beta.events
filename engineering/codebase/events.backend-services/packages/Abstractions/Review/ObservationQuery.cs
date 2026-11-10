namespace WoW.Two.Sdk.Beta.Be.Events.Review;

/// <summary>Filters and pages a catalog's observations, newest first.</summary>
public sealed record ObservationQuery
{
    /// <summary>Gets the state to list.</summary>
    public ObservationState? State { get; init; }

    /// <summary>Gets the event whose observations to list.</summary>
    public Guid? EventId { get; init; }

    /// <summary>Gets the cursor a previous page returned.</summary>
    public string? Cursor { get; init; }

    /// <summary>Gets the page size, at most <see cref="EventsConstants.MaxPageSize"/>.</summary>
    public int Limit { get; init; } = EventsConstants.DefaultPageSize;
}
