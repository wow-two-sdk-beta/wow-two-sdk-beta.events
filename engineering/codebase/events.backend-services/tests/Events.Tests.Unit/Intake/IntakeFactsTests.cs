using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Unit.Intake;

/// <summary>Reading a record's facts as one source delivers them: its default country, the cities it covers and its default
/// kind, over the mapper's reading.</summary>
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

    private static string Locality(string city) =>
        """{"schema":"wow2.event.v1","title":"Show","schemaOrg":{"location":{"address":{"addressLocality":"CITY"}}}}""".Replace("CITY", city);

    private static EventsIntakeSourceOptions Source(params string[] cities)
    {
        var source = new EventsIntakeSourceOptions { Name = "afisha-fashion", Catalog = "ocharo-fashion", DefaultCountry = "UZ" };
        foreach (var city in cities)
            source.DefaultCities.Add(city);
        return source;
    }

    private static EventFacts Facts(EventsIntakeSourceOptions source, string json)
    {
        using var document = JsonDocument.Parse(json);
        IntakeProcessor.TryFacts(source, document.RootElement, Kinds, null, out var facts, out var reason).Should().BeTrue(reason);
        return facts;
    }
}
