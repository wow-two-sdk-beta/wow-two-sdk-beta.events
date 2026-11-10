using System.Globalization;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;

namespace Events.Api.Shared;

/// <summary>Maps query values as the body's JSON reads them: enums by their camelCase wire names (<c>movedOnline</c>),
/// days as <c>yyyy-MM-dd</c>.</summary>
internal static class WireQueryMapper
{
    /// <summary>Maps an enum's wire name; an empty value is no filter.</summary>
    /// <returns>The validation error for an unknown name; none otherwise.</returns>
    public static AppError? TryEnum<TEnum>(string name, string? value, out TEnum? parsed) where TEnum : struct, Enum
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            parsed = JsonSerializer.Deserialize<TEnum>(JsonSerializer.Serialize(value), JsonOptionsConstants.Default);
            return null;
        }
        catch (JsonException)
        {
            return EventsErrors.Invalid(name, $"'{value}' is not a known value");
        }
    }

    /// <summary>Maps a day; an empty value is no filter.</summary>
    /// <returns>The validation error for a value that is no <c>yyyy-MM-dd</c> day; none otherwise.</returns>
    public static AppError? TryDay(string name, string? value, out DateOnly? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            parsed = day;
            return null;
        }

        return EventsErrors.Invalid(name, "a day as yyyy-MM-dd");
    }

    /// <summary>Requires a revision read, which a delete or a rejection names in its query.</summary>
    /// <returns>The validation error when it is missing; none otherwise.</returns>
    public static AppError? RequireRevision(int? expectedRevision, out int revision)
    {
        revision = expectedRevision ?? 0;
        return expectedRevision is null ? EventsErrors.Invalid("expectedRevision", "required") : null;
    }
}
