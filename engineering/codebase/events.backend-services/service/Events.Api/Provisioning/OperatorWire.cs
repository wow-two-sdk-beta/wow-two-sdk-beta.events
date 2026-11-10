using System.Text.Json;
using System.Text.Json.Serialization;

namespace Events.Api.Provisioning;

/// <summary>The operator verbs' output conventions: JSON on one line, and a reason word for a refusal.</summary>
internal static class OperatorWire
{
    /// <summary>Gets the JSON the verbs print: the API's camelCase, enums as camelCase words, nulls left out.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>Reads the database the verbs run against: <c>DB_CONNECTION</c>, else <c>DatabaseSettings:ConnectionString</c>.</summary>
    /// <param name="configuration">The settings.</param>
    public static string? ConnectionString(IConfiguration configuration) =>
        configuration["DB_CONNECTION"] is { Length: > 0 } alias ? alias : configuration["DatabaseSettings:ConnectionString"];

    /// <summary>Turns an error message into a reason word of at most 60 characters, letters, digits and hyphens only.</summary>
    /// <param name="message">The error message.</param>
    public static string Reason(string message)
    {
        var words = new string([.. message.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);
        var collapsed = string.Join('-', words.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > 60 ? collapsed[..60] : collapsed;
    }

    /// <summary>Writes a flag as the verbs print it.</summary>
    /// <param name="value">The flag.</param>
    public static string Bool(bool value) => value ? "true" : "false";

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
