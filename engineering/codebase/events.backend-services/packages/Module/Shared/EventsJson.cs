using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Reads and writes the module's jsonb columns in the wire's shape: camelCase names, camelCase enum labels.</summary>
internal static class EventsJson
{
    /// <summary>Gets the options every jsonb column is written and read with.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>Serializes a value.</summary>
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Deserializes a value, failing loudly on a column the module did not write.</summary>
    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"A stored {typeof(T).Name} is null.");

    /// <summary>Parses a stored JSON value into a detached element; none for a null column.</summary>
    public static JsonElement? Element(string? json)
    {
        if (json is null)
            return null;

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Writes an element; none for a missing or JSON-null element.</summary>
    public static string? Write(JsonElement? element) =>
        element is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } value ? value.GetRawText() : null;

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
