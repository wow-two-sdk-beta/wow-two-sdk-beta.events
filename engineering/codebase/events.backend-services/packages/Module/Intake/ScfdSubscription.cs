using System.Globalization;
using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Represents what intake checks of an SCFD subscription before it pulls: the filter that decides what SCFD
/// delivers, the schema it renders and its state (<c>GET api/subscriptions/{id}</c>).</summary>
/// <remarks>
///   - under <c>auto</c> review with the <c>filter</c> classifier, every delivered record publishes, so the source's
///     <c>Filter</c> must be the subscription's own <c>filterKey</c>: a subscription created without it, or on another
///     schema, would otherwise publish everything it delivers
///   - a source bound to an SCFD subscription selector names it <c>selector:{name}</c>: the subscription must be of kind
///     <c>selector</c> with that name (it carries no <c>filterKey</c>; the selector is its filter)
/// </remarks>
/// <param name="FilterKey">The subscription's filter; none delivers everything.</param>
/// <param name="Schema">The schema the subscription's records carry.</param>
/// <param name="State">The subscription's state, lowercase: <c>active</c>, <c>suspended</c>.</param>
/// <param name="Kind">The subscription's kind when it is a selector (<see cref="SelectorKind"/>); none for any other kind,
/// because intake binds by kind only to a selector.</param>
/// <param name="Name">The subscription's name, which a selector subscription carries and Events binds to as
/// <c>selector:{name}</c>; none for the other kinds.</param>
public sealed record ScfdSubscription(string? FilterKey, string? Schema, string? State, string? Kind = null, string? Name = null)
{
    /// <summary>Holds the state a subscription must be in for intake to pull.</summary>
    public const string ActiveState = "active";

    /// <summary>Holds the kind of a subscription that follows a selector over SCFD's tags and sources.</summary>
    public const string SelectorKind = "selector";

    /// <summary>Holds the prefix of a source's <c>Filter</c> that binds the source to a selector subscription by name.</summary>
    public const string SelectorFilterPrefix = "selector:";

    private const int SelectorKindNumber = 3;
    private const int MaxNameLength = 64;

    /// <summary>Reads a subscription from SCFD's answer, its <c>data</c> object; the state as a camelCase label or as
    /// SCFD's <c>SubscriptionState</c> number (Active 0, Suspended 1), the kind as a label or as <c>SubscriptionKind</c>'s
    /// number (<c>Selector</c> 3), kept only when it is a selector.</summary>
    /// <param name="data">The subscription object.</param>
    /// <returns>The subscription; none when the answer is not an object.</returns>
    public static ScfdSubscription? Read(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return null;

        var state = data.TryGetProperty("state", out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim().ToLowerInvariant(),
            JsonValueKind.Number when value.TryGetInt32(out var number) => number switch
            {
                0 => ActiveState,
                1 => "suspended",
                _ => number.ToString(CultureInfo.InvariantCulture),
            },
            _ => null,
        } : null;
        var kind = data.TryGetProperty("kind", out var label) ? label.ValueKind switch
        {
            JsonValueKind.String => label.GetString()?.Trim().ToLowerInvariant(),
            JsonValueKind.Number when label.TryGetInt32(out var number) && number == SelectorKindNumber => SelectorKind,
            _ => null,
        } : null;
        return new ScfdSubscription(Text(data, "filterKey"), Text(data, "schema"), state, kind == SelectorKind ? SelectorKind : null, Text(data, "name"));
    }

    /// <summary>Names the first way the subscription differs from what the source expects, or none when it matches:
    /// <c>schema-mismatch</c>, <c>state-{state}</c>, or <c>filter-mismatch</c> when the source names a filter the
    /// subscription does not carry, or, for <c>selector:{name}</c>, a subscription that is not the selector of that
    /// name.</summary>
    /// <param name="source">The intake source.</param>
    public string? MismatchWith(EventsIntakeSourceOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!string.Equals(Schema, EventsConstants.EventSchema, StringComparison.Ordinal))
            return "schema-mismatch";
        if (!string.Equals(State, ActiveState, StringComparison.Ordinal))
            return "state-" + Safe(State);
        return ExpectedFilter(source) is { } filter && !CarriesFilter(filter)
            ? "filter-mismatch"
            : null;
    }

    /// <summary>Tells whether the subscription carries the filter a source names: a plain filter is the subscription's
    /// <c>filterKey</c>; <c>selector:{name}</c> is a subscription of kind <c>selector</c> with that name.</summary>
    private bool CarriesFilter(string filter) =>
        filter.StartsWith(SelectorFilterPrefix, StringComparison.Ordinal)
            ? string.Equals(Kind, SelectorKind, StringComparison.Ordinal)
              && string.Equals(Name, filter[SelectorFilterPrefix.Length..].Trim(), StringComparison.Ordinal)
            : string.Equals(FilterKey, filter, StringComparison.Ordinal);

    /// <summary>Describes the subscription against the source's expectation, for a log line or a recorded failure: names
    /// and states only, never a token, a host or a body.</summary>
    /// <param name="source">The intake source.</param>
    public string Describe(EventsIntakeSourceOptions source) =>
        $"expects filter '{ExpectedFilter(source) ?? "(any)"}', schema {EventsConstants.EventSchema}, state {ActiveState}; " +
        $"SCFD has filter '{FilterKey ?? "(none)"}', kind '{Kind ?? "(not a selector)"}', name '{(Name is null ? "(none)" : Safe(Name, MaxNameLength))}', " +
        $"schema '{Schema ?? "(none)"}', state '{State ?? "(none)"}'";

    /// <summary>Gets the filter the source expects its subscription to carry; none when it names none. A name after
    /// <see cref="SelectorFilterPrefix"/> asks for the selector subscription of that name.</summary>
    /// <param name="source">The intake source.</param>
    public static string? ExpectedFilter(EventsIntakeSourceOptions source) =>
        string.IsNullOrWhiteSpace(source?.Filter) ? null : source.Filter.Trim();

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static string Safe(string? value, int max = 32)
    {
        var safe = new string((value ?? string.Empty).Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').Take(max).ToArray());
        return safe.Length == 0 ? "unknown" : safe;
    }
}
