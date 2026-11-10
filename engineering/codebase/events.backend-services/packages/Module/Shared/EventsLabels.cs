using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Converts the contract's enums to and from the camelCase labels the text columns hold, as on the wire.</summary>
internal static class EventsLabels
{
    /// <summary>Gets an enum member's label, such as <c>movedOnline</c>.</summary>
    public static string Of<TEnum>(TEnum value) where TEnum : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    /// <summary>Parses a stored label.</summary>
    public static TEnum Parse<TEnum>(string label) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(label, ignoreCase: true);
}
