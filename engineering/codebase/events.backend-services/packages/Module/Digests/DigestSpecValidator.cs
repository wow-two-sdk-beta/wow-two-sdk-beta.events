using System.Text.RegularExpressions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Validates and normalizes a digest's fields: trimmed texts, lists without repeats, cities in lower case.</summary>
internal static partial class DigestSpecValidator
{
    private const int MaxSubjectLength = 256;
    private const int MaxListLength = EventsConstants.MaxListLength;

    /// <summary>Validates a spec against the channels the host registers.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="channels">The registered channels.</param>
    /// <param name="normalized">The normalized spec, when valid.</param>
    /// <returns>The first failure; none when valid.</returns>
    public static AppError? Validate(DigestSpec? spec, IReadOnlyCollection<string> channels, out DigestSpec normalized)
    {
        normalized = null!;
        if (spec is null)
            return EventsErrors.Invalid("digest", "required");

        var name = spec.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > EventsConstants.MaxNameLength)
            return EventsErrors.Invalid("name", $"1 to {EventsConstants.MaxNameLength} characters");

        var channel = spec.Channel?.Trim() ?? string.Empty;
        if (!ChannelPattern().IsMatch(channel) || !channels.Contains(channel))
            return EventsErrors.Invalid("channel", channels.Count == 0 ? "no channel is registered" : $"one of {string.Join(", ", channels.Order(StringComparer.Ordinal))}");

        if (ValidateSchedule(spec.Schedule, out var schedule) is { } scheduleError)
            return scheduleError;
        if (ValidateSelection(spec.Selection ?? new DigestSelection(), out var selection) is { } selectionError)
            return selectionError;
        if (ValidateRecipients(spec.Recipients, out var recipients) is { } recipientsError)
            return recipientsError;

        var language = spec.Language?.Trim() ?? string.Empty;
        if (!DigestLanguageConstants.All.Contains(language))
            return EventsErrors.Invalid("language", string.Join(", ", DigestLanguageConstants.All));
        if (spec.ItemCap is < 1 or > EventsConstants.MaxDigestItems)
            return EventsErrors.Invalid("itemCap", $"1 to {EventsConstants.MaxDigestItems}");

        var template = string.IsNullOrWhiteSpace(spec.LinkTemplate) ? null : spec.LinkTemplate.Trim();
        if (template is not null && !IsLinkTemplate(template))
            return EventsErrors.Invalid("linkTemplate", $"an absolute https or http URL of at most {EventsConstants.MaxUrlLength} characters, with {{id}}, {{key}} or {{catalog}}");

        normalized = spec with
        {
            Name = name,
            Channel = channel,
            Schedule = schedule,
            Selection = selection,
            Recipients = recipients,
            Language = language,
            LinkTemplate = template,
        };
        return null;
    }

    /// <summary>Tells whether a template, filled with sample values, is an absolute http or https URL.</summary>
    public static bool IsLinkTemplate(string template) =>
        template.Length <= EventsConstants.MaxUrlLength
        && Uri.TryCreate(DigestLinks.Fill(template, Guid.Empty, "key", "catalog"), UriKind.Absolute, out var url)
        && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp);

    private static AppError? ValidateSchedule(DigestSchedule? schedule, out DigestSchedule normalized)
    {
        normalized = null!;
        if (schedule is null)
            return EventsErrors.Invalid("schedule", "required");

        var days = schedule.Days ?? [];
        if (days.Count is 0 or > 7 || days.Any(day => !Enum.IsDefined(day)) || days.Distinct().Count() != days.Count)
            return EventsErrors.Invalid("schedule.days", "1 to 7 days of the week, each once");

        var times = (schedule.Times ?? []).Select(time => time?.Trim() ?? string.Empty).ToList();
        if (times.Count is 0 or > EventsConstants.MaxDigestTimes)
            return EventsErrors.Invalid("schedule.times", $"1 to {EventsConstants.MaxDigestTimes} local times");
        for (var index = 0; index < times.Count; index++)
        {
            if (!DigestOccurrences.TryReadTime(times[index], out _))
                return EventsErrors.Invalid($"schedule.times[{index}]", "a local time, HH:mm");
        }

        if (times.Distinct(StringComparer.Ordinal).Count() != times.Count)
            return EventsErrors.Invalid("schedule.times", "each time once");

        var zone = schedule.TimeZone?.Trim();
        if (!DigestOccurrences.TryFindZone(zone, out _))
            return EventsErrors.Invalid("schedule.timeZone", "an IANA time zone the host knows, such as Asia/Tashkent");

        normalized = new DigestSchedule
        {
            Days = days.OrderBy(day => ((int)day + 6) % 7).ToList(),
            Times = times.Order(StringComparer.Ordinal).ToList(),
            TimeZone = zone!,
        };
        return null;
    }

    private static AppError? ValidateSelection(DigestSelection selection, out DigestSelection normalized)
    {
        normalized = null!;
        if (selection.StartingWithinDays is < 0 or > EventsConstants.MaxDigestDaysAhead)
            return EventsErrors.Invalid("selection.startingWithinDays", $"0 to {EventsConstants.MaxDigestDaysAhead}");
        if (selection.DeadlineWithinDays is < 0 or > EventsConstants.MaxDigestDaysAhead)
            return EventsErrors.Invalid("selection.deadlineWithinDays", $"0 to {EventsConstants.MaxDigestDaysAhead}");

        var statuses = selection.Statuses ?? [];
        if (statuses.Any(status => !Enum.IsDefined(status)))
            return EventsErrors.Invalid("selection.statuses", "event statuses");

        var kinds = (selection.Kinds ?? []).Select(kind => kind?.Trim() ?? string.Empty).Distinct(StringComparer.Ordinal).ToList();
        if (kinds.Count > MaxListLength || kinds.Any(kind => !EventFactsValidator.KeyPattern().IsMatch(kind)))
            return EventsErrors.Invalid("selection.kinds", $"at most {MaxListLength} kind keys matching {EventsConstants.KeyPattern}");

        var cities = (selection.Cities ?? []).Select(city => city?.Trim().ToLowerInvariant() ?? string.Empty).Distinct(StringComparer.Ordinal).ToList();
        if (cities.Count > MaxListLength || cities.Any(city => city.Length is 0 or > EventsConstants.MaxNameLength))
            return EventsErrors.Invalid("selection.cities", $"at most {MaxListLength} names of 1 to {EventsConstants.MaxNameLength} characters");

        normalized = selection with
        {
            Statuses = statuses.Distinct().Order().ToList(),
            Kinds = kinds,
            Cities = cities,
        };
        return null;
    }

    private static AppError? ValidateRecipients(IReadOnlyList<DigestRecipient>? recipients, out List<DigestRecipient> normalized)
    {
        normalized = [];
        var given = recipients ?? [];
        if (given.Count is 0 or > EventsConstants.MaxDigestRecipients)
            return EventsErrors.Invalid("recipients", $"1 to {EventsConstants.MaxDigestRecipients} entries");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < given.Count; index++)
        {
            var recipient = given[index];
            var subject = string.IsNullOrWhiteSpace(recipient?.Subject) ? null : recipient.Subject.Trim();
            var role = string.IsNullOrWhiteSpace(recipient?.Role) ? null : recipient.Role.Trim();
            var language = string.IsNullOrWhiteSpace(recipient?.Language) ? null : recipient.Language.Trim();
            if ((subject is null) == (role is null))
                return EventsErrors.Invalid($"recipients[{index}]", "a subject or a role, not both");
            if (subject is not null && (subject.Length > MaxSubjectLength || subject.Any(char.IsWhiteSpace)))
                return EventsErrors.Invalid($"recipients[{index}].subject", $"a sub of at most {MaxSubjectLength} characters, no spaces");
            if (role is not null && !RolePattern().IsMatch(role))
                return EventsErrors.Invalid($"recipients[{index}].role", "a role key of at most 64 characters");
            if (language is not null && !DigestLanguageConstants.All.Contains(language))
                return EventsErrors.Invalid($"recipients[{index}].language", string.Join(", ", DigestLanguageConstants.All));
            if (!seen.Add(subject is null ? $"role:{role}" : $"sub:{subject}"))
                return EventsErrors.Invalid($"recipients[{index}]", "names an earlier recipient again");

            normalized.Add(new DigestRecipient { Subject = subject, Role = role, Language = language });
        }

        return null;
    }

    [GeneratedRegex(DigestChannelConstants.Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex ChannelPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.:-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex RolePattern();
}
