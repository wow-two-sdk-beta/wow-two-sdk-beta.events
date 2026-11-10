using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Review;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Reads SCFD's <c>wow2.event.v1</c> projection: its facts, honestly (what the source does not state stays
/// unknown), and its evidence (links, capture, mapping stamp).</summary>
/// <remarks>Sourced media and text stay evidence only: no image and no description becomes a fact, so nothing sourced
/// is republished (events-integration #50).</remarks>
internal static partial class ScfdEventMapper
{
    private const int MaxMappingVersionLength = 64;

    /// <summary>Reads the payload's schema.</summary>
    public static string? Schema(JsonElement data) => String(data, "schema");

    /// <summary>Reads the post the payload renders.</summary>
    public static Guid? PostId(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } value && Guid.TryParse(String(value, "postId"), out var id) ? id : null;

    /// <summary>Reads the post revision the payload renders.</summary>
    public static int? Revision(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("revision", out var revision)
        && revision.ValueKind == JsonValueKind.Number && revision.TryGetInt32(out var number) ? number : null;

    /// <summary>Reads the mapping stamp as one version: its schema, norm, rules and parser, at most 64 characters.</summary>
    public static string? MappingVersion(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty("mapping", out var mapping) || mapping.ValueKind != JsonValueKind.Object)
            return null;

        var parts = new[] { "schema", "norm", "rules", "parser" }.Select(name => String(mapping, name)).Where(part => !string.IsNullOrEmpty(part)).ToList();
        if (parts.Count == 0)
            return null;

        var version = string.Join('/', parts);
        return version.Length > MaxMappingVersionLength ? version[..MaxMappingVersionLength] : version;
    }

    /// <summary>Reads the source links behind the payload, with the newest capture when no link names it.</summary>
    public static List<ObservationLinkModel> Links(JsonElement? data)
    {
        var links = new List<ObservationLinkModel>();
        if (data is not { ValueKind: JsonValueKind.Object } value)
            return links;

        if (value.TryGetProperty("links", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in array.EnumerateArray().Where(link => link.ValueKind == JsonValueKind.Object))
            {
                links.Add(new ObservationLinkModel
                {
                    Url = String(link, "url"),
                    SourceId = String(link, "sourceId"),
                    CaptureId = String(link, "captureId"),
                    CapturedAtUtc = Time(link, "capturedAtUtc"),
                    SourceFields = link.TryGetProperty("sourceFields", out var fields) && fields.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                        ? fields.Clone()
                        : null,
                });
            }
        }

        if (value.TryGetProperty("capture", out var capture) && capture.ValueKind == JsonValueKind.Object
            && String(capture, "captureId") is { } captureId && links.All(link => link.CaptureId != captureId))
        {
            links.Add(new ObservationLinkModel { Url = String(capture, "url"), CaptureId = captureId, CapturedAtUtc = Time(capture, "capturedAtUtc") });
        }

        return links;
    }

    /// <summary>Maps the payload to facts, before the shared validator runs.</summary>
    /// <param name="data">The payload, an object of schema <c>wow2.event.v1</c>.</param>
    /// <param name="kinds">The catalog's kind keys.</param>
    /// <param name="facts">The facts, when readable.</param>
    /// <param name="reason">Why the payload is malformed, when it is.</param>
    /// <returns>Whether the payload is readable.</returns>
    public static bool TryMap(JsonElement data, IReadOnlyCollection<string> kinds, out EventFacts facts, out string reason)
    {
        facts = null!;
        reason = string.Empty;
        var title = String(data, "title")?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            reason = "no title";
            return false;
        }

        if (!TryWhen(data, "starts", out var starts, out reason) || !TryWhen(data, "ends", out var ends, out reason))
            return false;

        var locale = Locale(data);
        var kind = String(data, "kind")?.Trim().ToLowerInvariant();
        var schemaOrg = data.TryGetProperty("schemaOrg", out var original) && original.ValueKind == JsonValueKind.Object ? original : (JsonElement?)null;
        var contentLinks = data.TryGetProperty("contentLinks", out var content) && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray().Where(link => link.ValueKind == JsonValueKind.Object).ToList()
            : [];

        facts = new EventFacts
        {
            Title = new Dictionary<string, string>(StringComparer.Ordinal) { [locale ?? EventsConstants.UnknownLocale] = Fit(title, EventsConstants.MaxTitleLength)! },
            Kind = kind is not null && kinds.Contains(kind) ? kind : "other",
            Organizer = Organizer(data),
            Place = Place(data, schemaOrg),
            Attendance = Attendance(schemaOrg),
            Status = Status(schemaOrg),
            Starts = starts ?? new EventWhen(),
            Ends = ends,
            Offers = Offers(data),
            Website = Url(contentLinks.FirstOrDefault(link => IsLinkKind(link, "site", 4)), "url"),
            TicketUrl = Url(contentLinks.FirstOrDefault(link => IsLinkKind(link, "ticket", 0)), "url"),
            Languages = locale is null ? [] : [locale],
        };
        return true;
    }

    private static bool TryWhen(JsonElement data, string name, out EventWhen? when, out string reason)
    {
        when = null;
        reason = string.Empty;
        if (!data.TryGetProperty(name, out var date) || date.ValueKind == JsonValueKind.Null)
            return true;

        var value = date.ValueKind == JsonValueKind.Object ? String(date, "value")?.Trim() : null;
        var precision = date.ValueKind == JsonValueKind.Object ? String(date, "precision")?.Trim().ToLowerInvariant() : null;
        var match = value is null ? null : DatePattern().Match(value);
        string? read = null;
        DatePrecision kind = DatePrecision.Unknown;
        if (match is { Success: true })
        {
            var year = match.Groups["year"].Value;
            var month = match.Groups["month"];
            var day = match.Groups["day"];
            var hour = match.Groups["hour"];
            var minute = match.Groups["minute"];
            (read, kind) = precision switch
            {
                "year" => (year, DatePrecision.Year),
                "month" when month.Success => ($"{year}-{month.Value}", DatePrecision.Month),
                "day" when day.Success => ($"{year}-{month.Value}-{day.Value}", DatePrecision.Day),
                "hour" or "minute" or "second" when hour.Success =>
                    ($"{year}-{month.Value}-{day.Value}T{hour.Value}:{(minute.Success ? minute.Value : "00")}", DatePrecision.Minute),
                _ => (null, DatePrecision.Unknown),
            };
        }

        if (read is null)
        {
            reason = $"unreadable dates: {name} '{value}' at precision '{precision}'";
            return false;
        }

        when = new EventWhen { Value = read, Precision = kind, Certainty = DateCertainty.Announced };
        return true;
    }

    private static string? Locale(JsonElement data)
    {
        if (!data.TryGetProperty("language", out var language))
            return null;

        var code = language.ValueKind switch
        {
            JsonValueKind.Object => String(language, "code"),
            JsonValueKind.String => language.GetString(),
            _ => null,
        };
        code = code?.Trim().ToLowerInvariant();
        if (code is null || code == EventsConstants.UnknownLocale || !LanguagePattern().IsMatch(code))
            return null;

        var script = language.ValueKind == JsonValueKind.Object ? String(language, "script") : null;
        return script is not null && ScriptPattern().IsMatch(script) ? $"{code}-{script}" : code;
    }

    private static EventOrganizer? Organizer(JsonElement data)
    {
        if (!data.TryGetProperty("organizer", out var organizer) || organizer.ValueKind != JsonValueKind.Object)
            return null;

        var name = Fit(String(organizer, "name"), EventsConstants.MaxNameLength);
        return name is null ? null : new EventOrganizer { Name = name, Website = Url(organizer, "url") };
    }

    private static EventPlace? Place(JsonElement data, JsonElement? schemaOrg)
    {
        string? venue = null, address = null, street = null, city = null, region = null;
        double? latitude = null, longitude = null;
        if (data.TryGetProperty("place", out var place) && place.ValueKind == JsonValueKind.Object)
        {
            if (place.TryGetProperty("mentions", out var mentions) && mentions.ValueKind == JsonValueKind.Array)
            {
                foreach (var mention in mentions.EnumerateArray().Where(mention => mention.ValueKind == JsonValueKind.Object))
                {
                    var text = Fit(String(mention, "text"), EventsConstants.MaxNameLength);
                    if (text is null)
                        continue;

                    // SCFD writes the kind as a label or as LocationKind's number (City 0 … Address 5).
                    if (IsLinkKind(mention, "city", 0)) city ??= text;
                    else if (IsLinkKind(mention, "region", 1)) region ??= text;
                    else if (IsLinkKind(mention, "street", 3)) street ??= text;
                    else if (IsLinkKind(mention, "venue", 4)) venue ??= text;
                    else if (IsLinkKind(mention, "address", 5)) address ??= text;
                    (latitude, longitude) = latitude is null ? Geo(mention) : (latitude, longitude);
                }
            }

            var (placeLatitude, placeLongitude) = Geo(place);
            if (placeLatitude is not null)
                (latitude, longitude) = (placeLatitude, placeLongitude);
        }

        // The source's own schema.org location fills what the mentions leave out: Afisha's reader puts the city only in
        // addressLocality, and SCFD's mentions carry the venue and the street (events-dev, 2026-11-10).
        string? country = null;
        if (schemaOrg is { } original
            && original.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.Object)
        {
            venue ??= Fit(String(location, "name"), EventsConstants.MaxNameLength);
            if (location.TryGetProperty("address", out var postal) && postal.ValueKind == JsonValueKind.Object)
            {
                city ??= Fit(String(postal, "addressLocality"), EventsConstants.MaxNameLength);
                region ??= Fit(String(postal, "addressRegion"), EventsConstants.MaxNameLength);
                street ??= Fit(String(postal, "streetAddress"), EventsConstants.MaxNameLength);
                country = String(postal, "addressCountry")?.Trim().ToUpperInvariant() is { Length: 2 } code && code.All(char.IsAsciiLetterUpper)
                    ? code
                    : null;
            }
            else if (location.TryGetProperty("address", out var line) && line.ValueKind == JsonValueKind.String)
            {
                street ??= Fit(line.GetString(), EventsConstants.MaxNameLength);
            }

            if (latitude is null)
                (latitude, longitude) = Geo(location);
        }

        address ??= street;
        return venue is null && address is null && city is null && region is null && country is null && latitude is null
            ? null
            : new EventPlace { Venue = venue, Address = address, City = city, Region = region, Country = country, Latitude = latitude, Longitude = longitude };
    }

    /// <summary>Reads a holder's coordinates, as numbers or as numeric text (schema.org's GeoCoordinates allows both;
    /// Afisha's reader writes text).</summary>
    private static (double? Latitude, double? Longitude) Geo(JsonElement holder)
    {
        if (!holder.TryGetProperty("geo", out var geo) || geo.ValueKind != JsonValueKind.Object
            || Coordinate(geo, "latitude") is not { } lat || Coordinate(geo, "longitude") is not { } lon)
            return (null, null);

        return lat is >= -90 and <= 90 && lon is >= -180 and <= 180 ? (lat, lon) : (null, null);
    }

    private static double? Coordinate(JsonElement geo, string name) =>
        !geo.TryGetProperty(name, out var value) ? null
        : value.ValueKind == JsonValueKind.Number ? value.GetDouble()
        : value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed) ? parsed
        : null;

    private static List<EventOffer> Offers(JsonElement data)
    {
        var offers = new List<EventOffer>();
        if (!data.TryGetProperty("offers", out var prices) || prices.ValueKind != JsonValueKind.Array)
            return offers;

        foreach (var price in prices.EnumerateArray().Where(price => price.ValueKind == JsonValueKind.Object))
        {
            var amount = Decimal(price, "amount");
            var min = Decimal(price, "min") ?? amount;
            var max = Decimal(price, "max") ?? amount;
            var free = price.TryGetProperty("free", out var flag) && flag.ValueKind == JsonValueKind.True;
            if ((min is null && max is null && !free) || min < 0 || max < 0 || min > max)
                continue;

            var currency = String(price, "currency")?.Trim().ToUpperInvariant();
            offers.Add(new EventOffer
            {
                Kind = IsLinkKind(price, "role", "ticket", 1) ? OfferKind.Ticket : OfferKind.Other,
                MinPrice = min,
                MaxPrice = max,
                Currency = currency is { Length: 3 } && currency.All(char.IsAsciiLetterUpper) ? currency : null,
                Free = free,
            });
            if (offers.Count == EventsConstants.MaxListLength)
                break;
        }

        return offers;
    }

    private static EventAttendance Attendance(JsonElement? schemaOrg) =>
        (schemaOrg is { } original ? String(original, "eventAttendanceMode") : null) switch
        {
            { } mode when mode.EndsWith("OnlineEventAttendanceMode", StringComparison.Ordinal) => EventAttendance.Online,
            { } mode when mode.EndsWith("MixedEventAttendanceMode", StringComparison.Ordinal) => EventAttendance.Mixed,
            _ => EventAttendance.Offline,
        };

    private static EventStatus Status(JsonElement? schemaOrg) =>
        (schemaOrg is { } original ? String(original, "eventStatus") : null) switch
        {
            { } status when status.EndsWith("EventCancelled", StringComparison.Ordinal) => EventStatus.Cancelled,
            { } status when status.EndsWith("EventPostponed", StringComparison.Ordinal) => EventStatus.Postponed,
            { } status when status.EndsWith("EventRescheduled", StringComparison.Ordinal) => EventStatus.Rescheduled,
            { } status when status.EndsWith("EventMovedOnline", StringComparison.Ordinal) => EventStatus.MovedOnline,
            _ => EventStatus.Scheduled,
        };

    private static bool IsLinkKind(JsonElement element, string label, int number) => IsLinkKind(element, "kind", label, number);

    private static bool IsLinkKind(JsonElement element, string property, string label, int number) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var kind) && kind.ValueKind switch
        {
            JsonValueKind.String => string.Equals(kind.GetString(), label, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => kind.TryGetInt32(out var value) && value == number,
            _ => false,
        };

    private static string? Url(JsonElement element, string name)
    {
        var url = element.ValueKind == JsonValueKind.Object ? String(element, name)?.Trim() : null;
        return url is { Length: > 0 and <= EventsConstants.MaxUrlLength }
            && Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? url
                : null;
    }

    private static decimal? Decimal(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out _),
            JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            _ => false,
        }
            ? value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : decimal.Parse(value.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
            : null;

    private static DateTimeOffset? Time(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var at)
            ? at.ToUniversalTime()
            : null;

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Fit(string? text, int max)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > max ? trimmed[..max].TrimEnd() : trimmed;
    }

    [GeneratedRegex(@"^(?<year>\d{4})(-(?<month>\d{2})(-(?<day>\d{2})([T ](?<hour>\d{2})(:(?<minute>\d{2}))?)?)?)?")]
    private static partial Regex DatePattern();

    [GeneratedRegex("^[a-z]{2,3}$")]
    private static partial Regex LanguagePattern();

    [GeneratedRegex("^[A-Z][a-z]{3}$")]
    private static partial Regex ScriptPattern();
}
