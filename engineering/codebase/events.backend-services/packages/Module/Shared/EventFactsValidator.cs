using System.Text.RegularExpressions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

/// <summary>Validates and normalizes <see cref="EventFacts"/>: one rule set for curators, reviewers, imports and intake.</summary>
internal static partial class EventFactsValidator
{
    /// <summary>Validates facts; trims texts, lowercases tags and the kind, uppercases the country.</summary>
    /// <param name="facts">The facts to check.</param>
    /// <param name="kinds">The catalog's kind keys; empty accepts any key.</param>
    /// <param name="normalized">The normalized facts, when valid.</param>
    /// <returns>The first error, or none when valid.</returns>
    public static AppError? Validate(EventFacts facts, IReadOnlyCollection<string> kinds, out EventFacts normalized)
    {
        normalized = facts;
        if (facts is null)
            return EventsErrors.Invalid("facts", "required");

        var title = Localized(facts.Title);
        if (title.Count == 0)
            return EventsErrors.Invalid("facts.title", "needs a text in at least one locale");
        if (title.Values.Any(text => text.Length > EventsConstants.MaxTitleLength))
            return EventsErrors.Invalid("facts.title", $"at most {EventsConstants.MaxTitleLength} characters per locale");
        if (title.Keys.Any(locale => !LocalePattern().IsMatch(locale)))
            return EventsErrors.Invalid("facts.title", "locales are BCP 47 tags such as uz, ru, en or und");

        var summary = Localized(facts.Summary);
        if (summary.Values.Any(text => text.Length > EventsConstants.MaxSummaryLength))
            return EventsErrors.Invalid("facts.summary", $"at most {EventsConstants.MaxSummaryLength} characters per locale");

        if (facts.Key is { } key && !KeyPattern().IsMatch(key))
            return EventsErrors.Invalid("facts.key", EventsConstants.KeyPattern);

        var kind = (facts.Kind ?? "other").Trim().ToLowerInvariant();
        if (!KeyPattern().IsMatch(kind))
            return EventsErrors.Invalid("facts.kind", EventsConstants.KeyPattern);
        if (kinds.Count > 0 && !kinds.Contains(kind))
            return EventsErrors.Invalid("facts.kind", $"'{kind}' is not a kind of the catalog");

        var tags = (facts.Tags ?? []).Select(tag => tag.Trim().ToLowerInvariant()).Where(tag => tag.Length > 0).Distinct().ToList();
        if (tags.Count > EventsConstants.MaxListLength || tags.Any(tag => tag.Length > EventsConstants.MaxKeyLength))
            return EventsErrors.Invalid("facts.tags", $"at most {EventsConstants.MaxListLength} tags of {EventsConstants.MaxKeyLength} characters");

        if (!DateSpan.TryRead(facts.Starts, out var starts, out var startsError))
            return EventsErrors.Invalid("facts.starts", startsError!);
        if (!DateSpan.TryRead(facts.Ends, out var ends, out var endsError))
            return EventsErrors.Invalid("facts.ends", endsError!);
        if (ends is not null && (starts is null || ends.Value.Max < starts.Value.Min))
            return EventsErrors.Invalid("facts.ends", "never before the start, and only with a known start");

        var place = facts.Place;
        if (place is not null)
        {
            var country = place.Country?.Trim().ToUpperInvariant();
            if (country is not null && !CountryPattern().IsMatch(country))
                return EventsErrors.Invalid("facts.place.country", "an ISO 3166-1 alpha-2 code");
            if (Longer(place.Venue, place.Address, place.City, place.Region))
                return EventsErrors.Invalid("facts.place", $"each text at most {EventsConstants.MaxNameLength} characters");
            place = place with
            {
                Venue = Trim(place.Venue),
                Address = Trim(place.Address),
                City = Trim(place.City),
                Region = Trim(place.Region),
                Country = string.IsNullOrEmpty(country) ? null : country,
            };
        }

        var organizer = facts.Organizer;
        if (organizer is not null)
        {
            if (string.IsNullOrWhiteSpace(organizer.Name) || organizer.Name.Trim().Length > EventsConstants.MaxNameLength)
                return EventsErrors.Invalid("facts.organizer.name", $"1 to {EventsConstants.MaxNameLength} characters");
            if (!IsUrl(organizer.Website))
                return EventsErrors.Invalid("facts.organizer.website", "an http(s) URL");
            organizer = organizer with { Name = organizer.Name.Trim(), Website = Trim(organizer.Website) };
        }

        if (!IsUrl(facts.Website))
            return EventsErrors.Invalid("facts.website", "an http(s) URL");
        if (!IsUrl(facts.TicketUrl))
            return EventsErrors.Invalid("facts.ticketUrl", "an http(s) URL");
        if (!IsUrl(facts.OnlineUrl))
            return EventsErrors.Invalid("facts.onlineUrl", "an http(s) URL");

        var deadlines = facts.Deadlines ?? [];
        if (deadlines.Count > EventsConstants.MaxListLength)
            return EventsErrors.Invalid("facts.deadlines", $"at most {EventsConstants.MaxListLength}");
        for (var index = 0; index < deadlines.Count; index++)
        {
            if (deadlines[index].At is null)
                return EventsErrors.Invalid($"facts.deadlines[{index}].at", "required");
            if (!DateSpan.TryRead(deadlines[index].At, out _, out var deadlineError))
                return EventsErrors.Invalid($"facts.deadlines[{index}].at", deadlineError!);
            if (!IsUrl(deadlines[index].Url))
                return EventsErrors.Invalid($"facts.deadlines[{index}].url", "an http(s) URL");
        }

        var offers = facts.Offers ?? [];
        if (offers.Count > EventsConstants.MaxListLength)
            return EventsErrors.Invalid("facts.offers", $"at most {EventsConstants.MaxListLength}");
        for (var index = 0; index < offers.Count; index++)
        {
            var offer = offers[index];
            if (offer.MinPrice < 0 || offer.MaxPrice < 0 || offer.MinPrice > offer.MaxPrice)
                return EventsErrors.Invalid($"facts.offers[{index}]", "prices are not negative, and min is not above max");
            if (offer.Currency is { } currency && !CurrencyPattern().IsMatch(currency))
                return EventsErrors.Invalid($"facts.offers[{index}].currency", "an ISO 4217 code");
            if (offer.Terms is { Length: > EventsConstants.MaxTextLength })
                return EventsErrors.Invalid($"facts.offers[{index}].terms", $"at most {EventsConstants.MaxTextLength} characters");
            if (!IsUrl(offer.Url))
                return EventsErrors.Invalid($"facts.offers[{index}].url", "an http(s) URL");
        }

        var images = facts.Images ?? [];
        if (images.Count > EventsConstants.MaxListLength || images.Any(image => !IsUrl(image.Url) || string.IsNullOrWhiteSpace(image.Url)))
            return EventsErrors.Invalid("facts.images", $"at most {EventsConstants.MaxListLength}, each with an http(s) URL");

        var languages = (facts.Languages ?? []).Select(language => language.Trim()).Where(language => language.Length > 0).Distinct().ToList();
        if (languages.Any(language => !LocalePattern().IsMatch(language)))
            return EventsErrors.Invalid("facts.languages", "BCP 47 tags");

        normalized = facts with
        {
            Key = facts.Key,
            Title = title,
            Summary = summary,
            Kind = kind,
            Tags = tags,
            Place = place,
            Organizer = organizer,
            Starts = facts.Starts ?? new EventWhen(),
            Deadlines = deadlines,
            Offers = offers,
            Images = images,
            Languages = languages,
            Website = Trim(facts.Website),
            TicketUrl = Trim(facts.TicketUrl),
            OnlineUrl = Trim(facts.OnlineUrl),
        };
        return null;
    }

    private static Dictionary<string, string> Localized(IReadOnlyDictionary<string, string>? texts) =>
        (texts ?? new Dictionary<string, string>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.Ordinal);

    private static string? Trim(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static bool Longer(params string?[] texts) => texts.Any(text => text is { Length: > EventsConstants.MaxNameLength });

    private static bool IsUrl(string? url) =>
        string.IsNullOrWhiteSpace(url)
        || (url.Trim().Length <= EventsConstants.MaxUrlLength
            && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

    [GeneratedRegex(EventsConstants.KeyPattern)]
    internal static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex LocalePattern();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryPattern();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
