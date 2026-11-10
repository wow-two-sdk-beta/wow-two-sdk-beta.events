namespace WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

/// <summary>Represents an honest date: its value carries exactly its precision, and an unknown day is never invented.</summary>
public sealed record EventWhen
{
    /// <summary>Gets the value: <c>2026</c>, <c>2026-03</c>, <c>2026-03-14</c> or <c>2026-03-14T19:00</c> (local time, no
    /// offset); none when the precision is <see cref="DatePrecision.Unknown"/>.</summary>
    public string? Value { get; init; }

    /// <summary>Gets how much of the date is known; it must match the value's shape.</summary>
    public DatePrecision Precision { get; init; } = DatePrecision.Unknown;

    /// <summary>Gets how sure the date is.</summary>
    public DateCertainty Certainty { get; init; } = DateCertainty.Announced;
}
