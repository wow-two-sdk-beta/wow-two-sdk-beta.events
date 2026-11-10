namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Reads one page of an SCFD pull cursor off the wire: <c>{ data, next, hasMore }</c>.</summary>
internal sealed record ScfdCursorPage
{
    /// <summary>Gets the page's records, in log order.</summary>
    public IReadOnlyList<ScfdDeliveryRecord>? Data { get; init; }

    /// <summary>Gets the cursor of the next page.</summary>
    public string? Next { get; init; }

    /// <summary>Gets whether more records wait.</summary>
    public bool HasMore { get; init; }
}
