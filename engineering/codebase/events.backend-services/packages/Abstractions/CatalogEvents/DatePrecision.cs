namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Says how much of a date is known; a value never claims more than its precision.</summary>
public enum DatePrecision
{
    /// <summary>No date is known.</summary>
    Unknown,

    /// <summary>The year: <c>2026</c>.</summary>
    Year,

    /// <summary>The month: <c>2026-03</c>.</summary>
    Month,

    /// <summary>The day: <c>2026-03-14</c>.</summary>
    Day,

    /// <summary>The local time to the minute: <c>2026-03-14T19:00</c>.</summary>
    Minute,
}
