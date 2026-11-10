using System.Globalization;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Reads an <see cref="EventWhen"/> as the days it covers: a day itself, a month or a year whole.</summary>
/// <param name="Min">The first day covered.</param>
/// <param name="Max">The last day covered.</param>
internal readonly record struct DateSpan(DateOnly Min, DateOnly Max)
{
    private static readonly DateOnly Earliest = new(1900, 1, 1);
    private static readonly DateOnly Latest = new(2200, 12, 31);

    /// <summary>Reads a value at its precision; none for an unknown date. Fails when the value's shape does not match.</summary>
    /// <param name="when">The date.</param>
    /// <param name="span">The days covered, when known.</param>
    /// <param name="error">What is wrong, when the value is invalid.</param>
    /// <returns>Whether the value is valid.</returns>
    public static bool TryRead(EventWhen? when, out DateSpan? span, out string? error)
    {
        span = null;
        error = null;
        if (when is null)
            return true;

        var value = when.Value?.Trim();
        if (when.Precision == DatePrecision.Unknown)
        {
            if (!string.IsNullOrEmpty(value))
                error = "an unknown date carries no value";
            return error is null;
        }

        if (string.IsNullOrEmpty(value))
        {
            error = "a known date needs a value";
            return false;
        }

        DateOnly min;
        DateOnly max;
        switch (when.Precision)
        {
            case DatePrecision.Year when value.Length == 4 && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 1900 and <= 2200:
                min = new DateOnly(year, 1, 1);
                max = new DateOnly(year, 12, 31);
                break;
            case DatePrecision.Month when DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month):
                min = month;
                max = month.AddMonths(1).AddDays(-1);
                break;
            case DatePrecision.Day when DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day):
                min = max = day;
                break;
            case DatePrecision.Minute when DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local):
                min = max = DateOnly.FromDateTime(local);
                break;
            default:
                error = $"'{value}' does not match precision {EventsLabels.Of(when.Precision)}";
                return false;
        }

        if (min < Earliest || max > Latest)
        {
            error = "the date must fall between 1900 and 2200";
            return false;
        }

        span = new DateSpan(min, max);
        return true;
    }

    /// <summary>Reads a value already validated; none for an unknown date.</summary>
    public static DateSpan? Of(EventWhen? when) => TryRead(when, out var span, out _) ? span : null;
}
