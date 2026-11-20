using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Review;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Reads SCFD's <c>wow2.event.v1</c> projection: its facts, honestly (what the source does not state stays
/// unknown), and its evidence (links, capture, mapping stamp).</summary>
/// <remarks>Sourced media become <c>facts.images</c>: each image keeps its source URL, the external link that credits the
/// source, and SCFD's copy in Files (<c>fileRef</c>) when it holds one. The description and tags stay evidence only,
/// as before (events-integration #50).</remarks>
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

    /// <summary>Reads the post's state, lowercase: <c>live</c>, <c>suspect</c> or <c>gone</c>, as a label or as SCFD's
    /// <c>PostState</c> number (Live 0, Suspect 1, Gone 2); none when the payload names none.</summary>
    public static string? PostState(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("state", out var state)
            ? state.ValueKind switch
            {
                JsonValueKind.String => state.GetString()?.Trim().ToLowerInvariant(),
                JsonValueKind.Number when state.TryGetInt32(out var number) => number switch { 0 => "live", 1 => "suspect", 2 => "gone", _ => null },
                _ => null,
            }
            : null;

    /// <summary>Reads the post's state as an event source's state: <c>live</c> (a relisted link included), <c>suspect</c> or
    /// <c>gone</c>; none when the payload names none or another.</summary>
    public static EventSourceState? SourceState(JsonElement? data) =>
        PostState(data) switch
        {
            "live" or "relisted" => EventSourceState.Live,
            "suspect" => EventSourceState.Suspect,
            "gone" => EventSourceState.Gone,
            _ => null,
        };

    /// <summary>Reads when the source posted the post (<c>postedAt.value</c>), in UTC; a date with no time reads as that day's
    /// start in UTC. None when the payload names none or an unreadable one.</summary>
    public static DateTimeOffset? PostedAt(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("postedAt", out var posted)
        && String(posted, "value")?.Trim() is { } text && DatePattern().Match(text) is { Success: true } match && match.Groups["day"].Success
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at.ToUniversalTime()
            : null;

    /// <summary>Reads one string of the payload's top-level <c>context</c> object: the opaque defaults a selector subscription's
    /// source ref attaches to each record it delivers (harvest D9), such as <c>defaultKind</c>, <c>defaultCountry</c>,
    /// <c>defaultTimeZone</c>, <c>sourceGroup</c> and <c>sourceName</c>. Trimmed; none when the payload has no context, the
    /// key is missing or is not text, or the text is blank.</summary>
    /// <param name="data">The payload.</param>
    /// <param name="key">The context key, case-sensitive.</param>
    public static string? ContextValue(JsonElement? data, string key) =>
        data is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("context", out var context)
        && context.ValueKind == JsonValueKind.Object && String(context, key)?.Trim() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>Reads the post another post absorbed this one into, on a <c>post.merged</c> record; none otherwise.</summary>
    public static Guid? MergedInto(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } value && Guid.TryParse(String(value, "mergedInto"), out var survivor) ? survivor : null;

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

        var machine = MachineTitles(value).Select(title => title.Language).ToList();
        var titleLink = value.TryGetProperty("fieldSources", out var sources) ? String(sources, "title") : null;
        if (value.TryGetProperty("links", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            var objects = array.EnumerateArray().Where(link => link.ValueKind == JsonValueKind.Object).ToList();
            // The machine titles belong to the link the title came from (fieldSources.title), else to the first link.
            var origin = objects.FindIndex(link => titleLink is not null && String(link, "id") == titleLink);
            origin = origin < 0 ? 0 : origin;
            for (var index = 0; index < objects.Count; index++)
            {
                var link = objects[index];
                var fields = link.TryGetProperty("sourceFields", out var own) && own.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                    ? own.Clone()
                    : (JsonElement?)null;
                links.Add(new ObservationLinkModel
                {
                    Url = String(link, "url"),
                    SourceId = String(link, "sourceId"),
                    CaptureId = String(link, "captureId"),
                    CapturedAtUtc = Time(link, "capturedAtUtc"),
                    SourceFields = index == origin && machine.Count > 0 ? WithMachineTitles(fields, machine) : fields,
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
        var titles = new Dictionary<string, string>(StringComparer.Ordinal) { [locale ?? EventsConstants.UnknownLocale] = Fit(title, EventsConstants.MaxTitleLength)! };
        // SCFD's machine titles (harvest OE06: the English title its tagging pass wrote) sit beside the source's own title,
        // never over it: a source that already states that language keeps its own words.
        foreach (var (language, text) in MachineTitles(data))
        {
            if (!titles.Keys.Any(key => SameLanguage(key, language)))
                titles[language] = text;
        }

        var schemaOrg = data.TryGetProperty("schemaOrg", out var original) && original.ValueKind == JsonValueKind.Object ? original : (JsonElement?)null;
        var contentLinks = data.TryGetProperty("contentLinks", out var content) && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray().Where(link => link.ValueKind == JsonValueKind.Object).ToList()
            : [];

        facts = new EventFacts
        {
            Title = titles,
            Kind = kind is not null && kinds.Contains(kind) ? kind : "other",
            Organizer = Organizer(data),
            Place = Place(data, schemaOrg),
            Attendance = Attendance(schemaOrg),
            Status = Status(schemaOrg),
            Starts = starts ?? new EventWhen(),
            Ends = ends,
            Deadlines = Deadlines(data),
            Offers = Offers(data),
            Website = Website(data, contentLinks),
            TicketUrl = Url(contentLinks.FirstOrDefault(link => IsLinkKind(link, "ticket", 0)), "url"),
            Images = Images(data),
            Languages = locale is null ? [] : [locale],
        };
        return true;
    }

    /// <summary>Reads the payload's images (<c>media[]</c> items of kind <c>image</c>) in their order, at most
    /// <see cref="EventsConstants.MaxListLength"/>. Each keeps its source <c>url</c>, the credit link, and its
    /// <c>fileRef</c> when that is a Files file id (a GUID); an item with no safe https URL, or of another kind, is left
    /// out rather than failing the record.</summary>
    private static List<EventImage> Images(JsonElement data)
    {
        var images = new List<EventImage>();
        if (!data.TryGetProperty("media", out var media) || media.ValueKind != JsonValueKind.Array)
            return images;

        foreach (var item in media.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
        {
            if (!string.Equals(String(item, "kind"), "image", StringComparison.OrdinalIgnoreCase) || SafeHttpsUrl(item, "url") is not { } url)
                continue;

            images.Add(new EventImage { Url = url, FileRef = Guid.TryParse(String(item, "fileRef")?.Trim(), out var file) ? file.ToString("D") : null });
            if (images.Count == EventsConstants.MaxListLength)
                break;
        }

        return images;
    }

    /// <summary>Reads a URL that is https, has a host, carries no credentials and fits the URL limit; none otherwise.</summary>
    private static string? SafeHttpsUrl(JsonElement element, string name) =>
        Url(element, name) is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.Host.Length > 0
            ? url
            : null;

    /// <summary>Reads SCFD's machine titles (<c>titles[]</c>, each <c>{ text, language, machine: true }</c>): the language as
    /// a lowercase tag and the text fitted to the title limit, the first per language. A title not marked
    /// <c>machine</c>, or with an unreadable language or no text, is left out: only the source's own <c>title</c> stands
    /// for the source.</summary>
    public static List<(string Language, string Text)> MachineTitles(JsonElement data)
    {
        var titles = new List<(string Language, string Text)>();
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("titles", out var array) || array.ValueKind != JsonValueKind.Array)
            return titles;

        foreach (var title in array.EnumerateArray().Where(title => title.ValueKind == JsonValueKind.Object))
        {
            var language = String(title, "language")?.Trim();
            var text = Fit(String(title, "text"), EventsConstants.MaxTitleLength);
            if (!title.TryGetProperty("machine", out var machine) || machine.ValueKind != JsonValueKind.True
                || text is null || language is null || !TitleLanguagePattern().IsMatch(language))
                continue;

            var root = language.Split('-')[0];
            var tag = root.ToLowerInvariant() + language[root.Length..];
            if (tag == EventsConstants.UnknownLocale || titles.Any(known => known.Language == tag))
                continue;

            titles.Add((tag, text));
        }

        return titles;
    }

    /// <summary>Tells whether two locale keys name one language (<c>ru</c> and <c>ru-Cyrl</c> do).</summary>
    private static bool SameLanguage(string key, string language) =>
        string.Equals(key.Split('-')[0], language.Split('-')[0], StringComparison.OrdinalIgnoreCase);

    /// <summary>Adds <c>machineTitles</c> (the machine-made titles' languages) to a link's source fields, so the observation
    /// says which of its titles a machine wrote; a link's own array of fields is kept under <c>fields</c>.</summary>
    private static JsonElement WithMachineTitles(JsonElement? fields, IReadOnlyList<string> languages)
    {
        var node = fields is { ValueKind: JsonValueKind.Object } own
            ? JsonNode.Parse(own.GetRawText())!.AsObject()
            : new JsonObject();
        if (fields is { ValueKind: JsonValueKind.Array } list)
            node["fields"] = JsonNode.Parse(list.GetRawText());
        node["machineTitles"] = new JsonArray([.. languages.Select(language => (JsonNode?)language)]);
        return JsonSerializer.SerializeToElement(node);
    }

    /// <summary>Reads the event's own website: the first <c>site</c> content link on a host of its own. SCFD marks every
    /// in-page link <c>site</c>, so the aggregator's venue page, a social profile or a map pin reads as one too; the
    /// website is none of these, and none is the honest answer when no link is left (events-integration E06).</summary>
    private static string? Website(JsonElement data, List<JsonElement> contentLinks)
    {
        var sourceHosts = Links(data).Select(link => HostOf(link.Url)).OfType<string>().ToList();
        foreach (var link in contentLinks.Where(link => IsLinkKind(link, "site", 4)))
        {
            if (Url(link, "url") is { } url && HostOf(url) is { } host
                && !sourceHosts.Any(known => SameSite(host, known)) && !IsSocialOrMap(host, new Uri(url).AbsolutePath))
                return url;
        }

        return null;
    }

    /// <summary>Hosts of social networks and messengers: a profile there is a contact, never an event's website.</summary>
    private static readonly string[] SocialHosts =
    [
        "instagram.com", "facebook.com", "fb.com", "fb.me", "t.me", "telegram.me", "telegram.org", "telegram.dog", "vk.com", "vk.ru", "ok.ru",
        "youtube.com", "youtu.be", "tiktok.com", "x.com", "twitter.com", "threads.net", "linkedin.com", "wa.me", "whatsapp.com", "viber.com",
        "discord.gg", "signal.me",
    ];

    /// <summary>Hosts of map services, whole: a pin is a place, never an event's website.</summary>
    private static readonly string[] MapHosts = ["2gis.com", "2gis.uz", "2gis.ru", "openstreetmap.org", "waze.com"];

    private static string? HostOf(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? (uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host).ToLowerInvariant()
            : null;

    /// <summary>Tells whether two hosts are one site: equal, or one a subdomain of the other.</summary>
    private static bool SameSite(string host, string other) =>
        host == other || host.EndsWith("." + other, StringComparison.Ordinal) || other.EndsWith("." + host, StringComparison.Ordinal);

    private static bool IsSocialOrMap(string host, string path) =>
        SocialHosts.Concat(MapHosts).Any(known => SameSite(host, known))
        || host.StartsWith("maps.", StringComparison.Ordinal)
        || ((host.StartsWith("google.", StringComparison.Ordinal) || host.Contains(".google.", StringComparison.Ordinal)
                || host.StartsWith("yandex.", StringComparison.Ordinal) || host.Contains(".yandex.", StringComparison.Ordinal) || host == "goo.gl")
            && (path.StartsWith("/maps", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/navi", StringComparison.OrdinalIgnoreCase)));

    /// <summary>Reads the start's IANA time zone: the source's zone, when the start value states an offset and that offset
    /// is the zone's own on that date (SCFD keeps the offset the page stated; Events' dates are local and carry none).
    /// None when the source names no zone or an unknown one, the start has no time or offset, or the offset is another
    /// zone's: a guessed zone would move the event's hour.</summary>
    /// <param name="data">The payload.</param>
    /// <param name="zone">The source's zone, such as <c>Asia/Tashkent</c>.</param>
    public static string? TimeZone(JsonElement data, string? zone)
    {
        if (string.IsNullOrWhiteSpace(zone) || !data.TryGetProperty("starts", out var starts) || String(starts, "value")?.Trim() is not { } value
            || !OffsetPattern().IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            || !TimeZoneInfo.TryFindSystemTimeZoneById(zone.Trim(), out var info))
            return null;

        return info.GetUtcOffset(at) == at.Offset ? zone.Trim() : null;
    }

    /// <summary>Reads the deadlines the source states (<c>deadlines[]</c>, harvest E07): each one's kind and when, at its
    /// own precision, at most <see cref="EventsConstants.MaxListLength"/>. A deadline whose date is unreadable is left out
    /// rather than failing the record: the event stands without it.</summary>
    private static List<EventDeadline> Deadlines(JsonElement data)
    {
        var deadlines = new List<EventDeadline>();
        if (!data.TryGetProperty("deadlines", out var array) || array.ValueKind != JsonValueKind.Array)
            return deadlines;

        foreach (var deadline in array.EnumerateArray().Where(deadline => deadline.ValueKind == JsonValueKind.Object))
        {
            if (!TryReadWhen(deadline, "deadline", out var at, out _) || at is null)
                continue;

            deadlines.Add(new EventDeadline { Kind = DeadlineKindOf(String(deadline, "kind")), At = at });
            if (deadlines.Count == EventsConstants.MaxListLength)
                break;
        }

        return deadlines;
    }

    /// <summary>Reads a deadline's kind label; SCFD sends <c>application</c> today, and a label Events does not know reads
    /// as <see cref="DeadlineKind.Other"/>.</summary>
    private static DeadlineKind DeadlineKindOf(string? label) =>
        label?.Trim().ToLowerInvariant().Replace("_", "-", StringComparison.Ordinal) switch
        {
            "application" => DeadlineKind.Application,
            "registration" => DeadlineKind.Registration,
            "early-bird" or "earlybird" => DeadlineKind.EarlyBird,
            "submission" => DeadlineKind.Submission,
            "payment" => DeadlineKind.Payment,
            _ => DeadlineKind.Other,
        };

    private static bool TryWhen(JsonElement data, string name, out EventWhen? when, out string reason)
    {
        when = null;
        reason = string.Empty;
        if (!data.TryGetProperty(name, out var date) || date.ValueKind == JsonValueKind.Null)
            return true;

        return TryReadWhen(date, name, out when, out reason);
    }

    /// <summary>Reads one SCFD date, <c>{ value, precision, certainty }</c>, at the precision it states. A <c>certainty</c> of
    /// <c>estimated</c> (an AI-estimated date of an undated post) reads as <see cref="DateCertainty.Estimated"/>, so it shows as
    /// approximate; <c>stated</c>, absent or any other value reads as <see cref="DateCertainty.Announced"/>.</summary>
    private static bool TryReadWhen(JsonElement date, string name, out EventWhen? when, out string reason)
    {
        when = null;
        reason = string.Empty;
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

        var certainty = String(date, "certainty")?.Trim().Equals("estimated", StringComparison.OrdinalIgnoreCase) == true
            ? DateCertainty.Estimated
            : DateCertainty.Announced;
        when = new EventWhen { Value = read, Precision = kind, Certainty = certainty };
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
        // One Latin spelling per known city, so a sourced event groups with the catalog's own events; the source's
        // spelling stays in the observation's raw payload, and a city the table does not know passes through.
        city = city is null ? null : CityNames.Canonical(city);
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

    [GeneratedRegex(@"T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex OffsetPattern();

    [GeneratedRegex("^[a-z]{2,3}$")]
    private static partial Regex LanguagePattern();

    [GeneratedRegex("^[A-Z][a-z]{3}$")]
    private static partial Regex ScriptPattern();

    [GeneratedRegex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex TitleLanguagePattern();
}
