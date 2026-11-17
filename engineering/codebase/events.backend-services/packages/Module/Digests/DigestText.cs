using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

/// <summary>Renders a digest's events in one language as Telegram's HTML subset: a bold head, then each event's title (its
/// link when the digest has one), dates honest about their precision, city and nearest open deadline.</summary>
/// <remarks>Text outside tags escapes <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c> and <c>&quot;</c>; a message stops listing
/// before it would pass <see cref="EventsConstants.MaxDigestTextLength"/> characters and says how many more matched.</remarks>
internal static class DigestText
{
    private const int Reserve = 80;

    /// <summary>Renders a message.</summary>
    /// <param name="name">The digest's name.</param>
    /// <param name="day">The occurrence's local day.</param>
    /// <param name="items">The events, in order.</param>
    /// <param name="matched">How many events matched, the cap's cut included.</param>
    /// <param name="language">The language.</param>
    /// <returns>The text.</returns>
    public static string Render(string name, DateOnly day, IReadOnlyList<DigestItemModel> items, int matched, string language)
    {
        var words = DigestWords.For(language);
        var text = new StringBuilder()
            .Append("<b>").Append(Escape(name)).Append("</b>\n")
            .Append(Escape(Day(day, words))).Append('\n');

        var listed = 0;
        foreach (var item in items)
        {
            var block = Item(listed + 1, item, language, words);
            if (text.Length + block.Length + Reserve > EventsConstants.MaxDigestTextLength)
                break;

            text.Append(block);
            listed++;
        }

        if (matched > listed)
            text.Append('\n').Append(Escape(string.Format(CultureInfo.InvariantCulture, words.More, matched - listed)));

        return text.ToString();
    }

    /// <summary>Picks an event's title in a language: the language, its base, the unknown locale, then any.</summary>
    public static string Title(IReadOnlyDictionary<string, string> title, string language)
    {
        var root = language.Split('-')[0];
        foreach (var locale in new[] { language, root, EventsConstants.UnknownLocale, DigestLanguageConstants.English, DigestLanguageConstants.Russian, "uz" })
        {
            if (title.TryGetValue(locale, out var text) && !string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return title.Values.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))?.Trim() ?? string.Empty;
    }

    /// <summary>Escapes text for Telegram's HTML subset.</summary>
    public static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>Writes a date at its precision: a year, a month, a day, or a day and time; an estimate says so.</summary>
    public static string When(EventWhen when, DigestWords words)
    {
        var value = when.Value?.Trim();
        string? text = null;
        switch (when.Precision)
        {
            case DatePrecision.Year:
                text = value;
                break;
            case DatePrecision.Month when DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month):
                text = words.Month(month.Month, month.Year);
                break;
            case DatePrecision.Day when DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day):
                text = Day(day, words);
                break;
            case DatePrecision.Minute when DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local):
                text = $"{Day(DateOnly.FromDateTime(local), words)}, {local:HH\\:mm}";
                break;
        }

        if (text is null)
            return words.DateUnknown;

        return when.Certainty == DateCertainty.Estimated ? $"{text} {words.Estimated}" : text;
    }

    private static string Day(DateOnly day, DigestWords words) => words.Day(day.Day, day.Month, day.Year);

    private static string Item(int number, DigestItemModel item, string language, DigestWords words)
    {
        var title = Escape(Title(item.Title, language));
        var block = new StringBuilder("\n").Append(number.ToString(CultureInfo.InvariantCulture)).Append(". <b>");
        if (item.Url is { } url)
            block.Append("<a href=\"").Append(Escape(url)).Append("\">").Append(title).Append("</a>");
        else
            block.Append(title);

        block.Append("</b>");
        if (item.IsNew)
            block.Append(" · <i>").Append(Escape(words.New)).Append("</i>");

        var dates = When(item.Starts, words);
        if (item.Ends is { Precision: not DatePrecision.Unknown } ends)
        {
            var end = When(ends, words);
            if (!string.Equals(end, dates, StringComparison.Ordinal))
                dates = $"{dates} – {end}";
        }

        block.Append('\n').Append(Escape(dates));
        if (!string.IsNullOrWhiteSpace(item.City))
            block.Append(" · ").Append(Escape(item.City.Trim()));
        if (item.Status != EventStatus.Scheduled)
            block.Append(" · <i>").Append(Escape(words.Status(item.Status))).Append("</i>");

        if (item.NextDeadline is { } deadline)
        {
            block.Append('\n').Append(Escape($"{words.Deadline(deadline.Kind)}: {When(deadline.At, words)}"));
        }

        return block.Append('\n').ToString();
    }
}
