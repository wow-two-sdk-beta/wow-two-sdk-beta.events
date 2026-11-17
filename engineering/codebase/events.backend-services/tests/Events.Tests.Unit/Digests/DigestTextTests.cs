using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests;

namespace Events.Tests.Unit.Digests;

/// <summary>Covers a digest's text: dates honest about their precision in each language, Telegram HTML escaping, the
/// title's language fallback and the message's length cap.</summary>
public sealed class DigestTextTests
{
    [Theory]
    [InlineData("en", DatePrecision.Year, "2027", DateCertainty.Announced, "2027")]
    [InlineData("en", DatePrecision.Month, "2027-03", DateCertainty.Estimated, "March 2027 (estimated)")]
    [InlineData("en", DatePrecision.Day, "2027-03-14", DateCertainty.Announced, "14 March 2027")]
    [InlineData("en", DatePrecision.Minute, "2027-03-14T19:00", DateCertainty.Announced, "14 March 2027, 19:00")]
    [InlineData("ru", DatePrecision.Month, "2027-03", DateCertainty.Announced, "март 2027")]
    [InlineData("ru", DatePrecision.Day, "2027-03-14", DateCertainty.Estimated, "14 марта 2027 (ориентировочно)")]
    [InlineData("uz-Latn", DatePrecision.Day, "2027-03-14", DateCertainty.Announced, "14-mart 2027")]
    [InlineData("uz-Latn", DatePrecision.Unknown, null, DateCertainty.Announced, "Sana aniqlanmoqda")]
    public void When_ShouldWriteTheDateAtItsPrecision(string language, DatePrecision precision, string? value, DateCertainty certainty, string expected)
    {
        DigestText.When(new EventWhen { Value = value, Precision = precision, Certainty = certainty }, DigestWords.For(language)).Should().Be(expected);
    }

    [Fact]
    public void Render_ShouldEscapeText_AndMarkNewAndRescheduledEvents()
    {
        var item = Item("Fashion <Week> & \"Co\"") with
        {
            Ends = new EventWhen { Value = "2027-03-16", Precision = DatePrecision.Day },
            City = "Tashkent",
            Status = EventStatus.Rescheduled,
            IsNew = true,
            Url = "https://app.example.test/events?id=1&x=\"2\"",
        };

        var text = DigestText.Render("Ocharo & friends", new DateOnly(2027, 3, 1), [item], 1, DigestLanguageConstants.English);

        text.Should().Be(
            "<b>Ocharo &amp; friends</b>\n1 March 2027\n" +
            "\n1. <b><a href=\"https://app.example.test/events?id=1&amp;x=&quot;2&quot;\">Fashion &lt;Week&gt; &amp; &quot;Co&quot;</a></b> · <i>new</i>\n" +
            "14 March 2027 – 16 March 2027 · Tashkent · <i>new dates</i>\n");
    }

    [Fact]
    public void Title_ShouldFallBackFromTheLanguage_ToItsBase_ThenTheUnknownLocale_ThenAny()
    {
        DigestText.Title(new Dictionary<string, string> { ["uz"] = "Moda", ["ru"] = "Мода" }, "uz-Latn").Should().Be("Moda");
        DigestText.Title(new Dictionary<string, string> { ["und"] = "Fashion", ["ru"] = "Мода" }, "en").Should().Be("Fashion");
        DigestText.Title(new Dictionary<string, string> { ["fr"] = "Mode" }, "ru").Should().Be("Mode");
    }

    [Fact]
    public void Render_ShouldStayWithinTelegramsLimit_AndCountWhatItLeftOut()
    {
        var items = Enumerable.Range(1, 50).Select(index => Item($"Event {index} {new string('x', 150)}")).ToList();

        var text = DigestText.Render("Digest", new DateOnly(2027, 3, 1), items, 80, DigestLanguageConstants.English);

        text.Length.Should().BeLessThanOrEqualTo(EventsConstants.MaxDigestTextLength);
        var listed = items.Count(item => text.Contains(DigestText.Title(item.Title, "en"), StringComparison.Ordinal));
        listed.Should().BeInRange(10, 49);
        text.Should().EndWith($"\n…and {80 - listed} more");
    }

    private static DigestItemModel Item(string title) => new()
    {
        EventId = Guid.NewGuid(),
        Title = new Dictionary<string, string> { ["en"] = title },
        Starts = new EventWhen { Value = "2027-03-14", Precision = DatePrecision.Day },
    };
}
