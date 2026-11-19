using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Unit.Intake;

/// <summary>Reading a record's facts as one source delivers them: its default country, the cities it covers and its default
/// kind, over the mapper's reading, with the defaults a record's own payload <c>context</c> carries ahead of the source's.</summary>
public sealed class IntakeFactsTests
{
    private static readonly string[] Kinds = ["runway", "exhibition"];

    /// <summary>A UZ source with no list of cities stamps every record, so a foreign fair announced on it reads as a local
    /// event (events-integration E11); with its cities named, only those cities take the country.</summary>
    [Theory]
    [InlineData("Ташкент", "UZ")]
    [InlineData("Toshkent", "UZ")]
    [InlineData("TASHKENT", "UZ")]
    [InlineData("Самарканд", "UZ")]
    [InlineData("Париж", null)]
    [InlineData("Almaty", null)]
    public void TryFacts_ShouldGiveTheDefaultCountry_OnlyToACityOfTheDefaultCities(string city, string? country)
    {
        var facts = Facts(Source(cities: ["Ташкент", "Toshkent", "Tashkent", "Samarkand"]), Locality(city));

        facts.Place!.Country.Should().Be(country);
        facts.Place.City.Should().NotBeNull("the record's city stays as the mapper read it");
    }

    /// <summary>Spellings in any script of a city the table knows name one city, so one name covers them all.</summary>
    [Fact]
    public void TryFacts_ShouldMatchAKnownCityInEverySpelling_WhenTheListNamesItOnce()
    {
        var source = Source(cities: ["Tashkent", "Bukhara"]);

        Facts(source, Locality("Ташкент")).Place!.Country.Should().Be("UZ");
        Facts(source, Locality("Buxoro")).Place!.Country.Should().Be("UZ");
        Facts(source, Locality("Париж")).Place!.Country.Should().BeNull();
    }

    /// <summary>A city outside the table matches as the list wrote it, without case or apostrophes.</summary>
    [Fact]
    public void TryFacts_ShouldMatchAnUnknownCityWithoutCaseOrApostrophes()
    {
        var source = Source(cities: ["Qo'rg'ontepa"]);

        Facts(source, Locality("QO‘RG‘ONTEPA")).Place!.Country.Should().Be("UZ");
        Facts(source, Locality("Qorgontepa")).Place!.Country.Should().Be("UZ");
        Facts(source, Locality("Paris")).Place!.Country.Should().BeNull();
    }

    [Fact]
    public void TryFacts_ShouldGiveTheDefaultCountry_WhenTheRecordNamesNoCity()
    {
        var facts = Facts(Source(cities: ["Tashkent"]), """{"schema":"wow2.event.v1","title":"Show","place":{"mentions":[{"kind":"venue","text":"Humo Arena"}]}}""");

        facts.Place!.Country.Should().Be("UZ");
    }

    [Fact]
    public void TryFacts_ShouldGiveTheDefaultCountryToEveryCity_WhenTheSourceNamesNoCities()
    {
        Facts(Source(), Locality("Париж")).Place!.Country.Should().Be("UZ");
    }

    [Fact]
    public void TryFacts_ShouldKeepTheCountryTheRecordNames()
    {
        var facts = Facts(Source(cities: ["Tashkent"]), """
            {"schema":"wow2.event.v1","title":"Show",
             "schemaOrg":{"location":{"address":{"addressLocality":"Ташкент","addressCountry":"KZ"}}}}
            """);

        facts.Place!.Country.Should().Be("KZ");
    }

    /// <summary>An SCFD selector subscription's source refs attach a per-channel <c>defaultKind</c> that rides each record's
    /// payload <c>context</c> (harvest D9): it fills a record that maps to <c>other</c>, ahead of the source's own default.</summary>
    [Fact]
    public void TryFacts_ShouldTakeTheContextDefaultKind_BeforeTheSourceDefault()
    {
        var source = Source();
        source.DefaultKind = "runway";

        Facts(source, Titled("""{"defaultKind":"exhibition"}""")).Kind.Should().Be("exhibition", "the record's own channel names it");
        Facts(source, Titled(null)).Kind.Should().Be("runway", "no context: the source's default stands");
        Facts(Source(), Titled("""{"defaultKind":" Exhibition "}""")).Kind.Should().Be("exhibition", "a source with no default still takes the context's, trimmed and lowercased");
    }

    [Fact]
    public void TryFacts_ShouldKeepTheRecordsKind_OverTheContextDefault()
    {
        var source = Source();

        Facts(source, Titled("""{"defaultKind":"exhibition"}""", kind: "runway")).Kind.Should().Be("runway", "the record names its own kind");
        Facts(source, Titled("""{"defaultKind":"exhibition"}"""), new IntakeVerdict(true, "payload", "relevant", Kind: "runway")).Kind.Should().Be("runway", "the classifier's kind comes first");
        Facts(source, Titled("""{"defaultKind":"exhibition"}"""), new IntakeVerdict(true, "payload", "relevant", Kind: "unheard-of")).Kind.Should().Be("exhibition", "a classifier's kind the catalog lacks gives way");
    }

    [Fact]
    public void TryFacts_ShouldTakeContextCountryAndZone()
    {
        var source = Source();
        source.DefaultCountry = "UZ";
        source.DefaultTimeZone = "Europe/Paris";
        const string context = """{"defaultCountry":"KZ","defaultTimeZone":"Asia/Tashkent"}""";

        var facts = Facts(source, Starting(context, "2026-12-05T19:00:00+05:00"));

        facts.Place!.Country.Should().Be("KZ", "the record's channel names a country before the source's");
        facts.TimeZone.Should().Be("Asia/Tashkent", "the context's zone is the one whose offset the start states; the source's Paris is not");

        var bare = Source();
        bare.DefaultCountry = null;
        var named = Facts(bare, Starting(context, "2026-12-05T19:00:00+05:00"));
        named.Place!.Country.Should().Be("KZ", "a source with no country of its own takes the context's");
        named.TimeZone.Should().Be("Asia/Tashkent");

        var plain = Facts(source, Starting(null, "2026-12-05T19:00:00+05:00"));
        plain.Place!.Country.Should().Be("UZ", "no context: the source's country stands");
        plain.TimeZone.Should().BeNull("the source's Paris is not the start's offset");
    }

    [Fact]
    public void TryFacts_ShouldApplyTheContextCountry_OnlyToTheSourcesCities_AndKeepTheRecordsOwn()
    {
        var source = Source(cities: ["Tashkent"]);
        const string context = """{"defaultCountry":"KZ"}""";

        Facts(source, Locality("Ташкент", context)).Place!.Country.Should().Be("KZ");
        Facts(source, Locality("Париж", context)).Place!.Country.Should().BeNull("a city the source does not cover takes no default, whoever names it");
        Facts(source, """
            {"schema":"wow2.event.v1","title":"Show","context":{"defaultCountry":"KZ"},
             "schemaOrg":{"location":{"address":{"addressLocality":"Ташкент","addressCountry":"UZ"}}}}
            """).Place!.Country.Should().Be("UZ", "a country the record names stands");
    }

    /// <summary>A context value is opaque data from another system: one that does not fit is ignored, and the record still
    /// maps with the source's own settings, never quarantined.</summary>
    [Theory]
    [InlineData("""{"defaultCountry":"uz","defaultTimeZone":"Mars/Olympus","defaultKind":"unknown"}""")]
    [InlineData("""{"defaultCountry":"UZB","defaultTimeZone":"","defaultKind":""}""")]
    [InlineData("""{"defaultCountry":5,"defaultTimeZone":["Asia/Tashkent"],"defaultKind":{"kind":"exhibition"}}""")]
    public void TryFacts_ShouldIgnoreAnUnusableContextCountryZoneAndKind(string context)
    {
        var source = Source();
        source.DefaultKind = "runway";
        source.DefaultTimeZone = "Asia/Tashkent";

        var facts = Facts(source, Starting(context, "2026-12-05T19:00:00+05:00"));

        facts.Place!.Country.Should().Be("UZ");
        facts.TimeZone.Should().Be("Asia/Tashkent");
        facts.Kind.Should().Be("runway");
    }

    [Fact]
    public void TryFacts_ShouldIgnoreAnUnknownContextKind()
    {
        var source = Source();

        Facts(source, Titled("""{"defaultKind":"concert"}""")).Kind.Should().Be("other", "the catalog's taxonomy lacks it");
        source.DefaultKind = "exhibition";
        Facts(source, Titled("""{"defaultKind":"concert"}""")).Kind.Should().Be("exhibition", "an unknown context kind gives way to the source's default");
    }

    private static string Titled(string? context, string? kind = null) =>
        "{\"schema\":\"wow2.event.v1\",\"title\":\"Show\"" + (kind is null ? string.Empty : $",\"kind\":\"{kind}\"") + Context(context) + "}";

    private static string Starting(string? context, string starts) =>
        "{\"schema\":\"wow2.event.v1\",\"title\":\"Show\",\"starts\":{\"value\":\"" + starts + "\",\"precision\":\"minute\"}" + Context(context) + "}";

    private static string Context(string? context) => context is null ? string.Empty : ",\"context\":" + context;

    private static string Locality(string city, string context) =>
        """{"schema":"wow2.event.v1","title":"Show","context":CONTEXT,"schemaOrg":{"location":{"address":{"addressLocality":"CITY"}}}}"""
            .Replace("CITY", city).Replace("CONTEXT", context);

    private static string Locality(string city) =>
        """{"schema":"wow2.event.v1","title":"Show","schemaOrg":{"location":{"address":{"addressLocality":"CITY"}}}}""".Replace("CITY", city);

    private static EventsIntakeSourceOptions Source(params string[] cities)
    {
        var source = new EventsIntakeSourceOptions { Name = "afisha-fashion", Catalog = "ocharo-fashion", DefaultCountry = "UZ" };
        foreach (var city in cities)
            source.DefaultCities.Add(city);
        return source;
    }

    private static EventFacts Facts(EventsIntakeSourceOptions source, string json, IntakeVerdict? verdict = null)
    {
        using var document = JsonDocument.Parse(json);
        IntakeProcessor.TryFacts(source, document.RootElement, Kinds, verdict, out var facts, out var reason).Should().BeTrue(reason);
        return facts;
    }
}
