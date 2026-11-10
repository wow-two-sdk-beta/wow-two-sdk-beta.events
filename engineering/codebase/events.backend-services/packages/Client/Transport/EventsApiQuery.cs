using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Transport;

/// <summary>Builds a route's query string with the names the service reads: the query records' properties in camelCase,
/// enums by their wire names, dates as <c>yyyy-MM-dd</c>; an unset value stays out.</summary>
internal sealed class EventsApiQuery
{
    private readonly List<KeyValuePair<string, string?>> parameters = [];

    /// <summary>Adds a text value.</summary>
    public EventsApiQuery Add(string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            parameters.Add(new(name, value));
        return this;
    }

    /// <summary>Adds a whole number.</summary>
    public EventsApiQuery Add(string name, int? value) =>
        value is { } number ? Add(name, number.ToString(CultureInfo.InvariantCulture)) : this;

    /// <summary>Adds a day.</summary>
    public EventsApiQuery Add(string name, DateOnly? value) =>
        value is { } day ? Add(name, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : this;

    /// <summary>Adds a flag.</summary>
    public EventsApiQuery Add(string name, bool? value) =>
        value is { } flag ? Add(name, flag ? "true" : "false") : this;

    /// <summary>Adds an id.</summary>
    public EventsApiQuery Add(string name, Guid? value) =>
        value is { } id ? Add(name, id.ToString("D")) : this;

    /// <summary>Adds an enum by its wire name.</summary>
    public EventsApiQuery AddEnum<TEnum>(string name, TEnum? value) where TEnum : struct, Enum =>
        value is { } member ? Add(name, WireName(member)) : this;

    /// <summary>Appends the query to a route.</summary>
    public string ToRoute(string route) => parameters.Count == 0 ? route : QueryHelpers.AddQueryString(route, parameters);

    /// <summary>Maps an enum to its wire name, as the body's JSON writes it.</summary>
    public static string WireName<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonSerializer.Serialize(value, EventsApiTransport.Json).Trim('"');
}
