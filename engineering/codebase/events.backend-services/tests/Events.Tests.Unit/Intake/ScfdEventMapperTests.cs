using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Unit.Intake;

/// <summary>Reading SCFD's <c>wow2.event.v1</c> projection: honest facts, evidence kept, sourced media and text never
/// becoming facts.</summary>
public sealed class ScfdEventMapperTests
{
    private static readonly string[] Kinds = ["runway", "expo"];

    [Fact]
    public void TryMap_ShouldReadEachPrecision_AtItsOwnGrain()
    {
        var facts = Map("""
            {"schema":"wow2.event.v1","title":"Show",
             "starts":{"value":"2026-11-03T19:00:00+05:00","precision":"second"},
             "ends":{"value":"2026-11","precision":"month"}}
            """);

        facts.Starts.Should().Be(new EventWhen { Value = "2026-11-03T19:00", Precision = DatePrecision.Minute, Certainty = DateCertainty.Announced });
        facts.Ends.Should().Be(new EventWhen { Value = "2026-11", Precision = DatePrecision.Month, Certainty = DateCertainty.Announced });
    }

    /// <summary>The live Afisha record on dev (2026-11-10), before and after SCFD turned its enums into strings (harvest
    /// v0.1.12), in its real shape: SCFD's mentions name the venue and the address, the city lives only in schema.org's
    /// addressLocality, and nothing names a country; second precision with an offset, no organizer.</summary>
    /// <param name="venueKind">The venue mention's kind: LocationKind's number, or its label.</param>
    /// <param name="addressKind">The address mention's kind.</param>
    [Theory]
    [InlineData("4", "5")]
    [InlineData("\"venue\"", "\"address\"")]
    public void TryMap_ShouldReadTheLiveAfishaRecord_WithNumberOrStringEnums(string venueKind, string addressKind)
    {
        var facts = Map($$$$"""
            {"schema":"wow2.event.v1","title":"Показ мод The Fashion Show Line 2026",
             "starts":{"value":"2026-11-21T17:00+05:00","precision":"second"},
             "postedAt":{"value":"2026-10-14","precision":"day"},
             "organizer":null,
             "place":{"mentions":[
               {"kind":{{{{venueKind}}}},"text":"Дворец творчества молодёжи"},
               {"kind":{{{{addressKind}}}},"text":"ул. Мустакиллик, 2"}],
               "geo":{"latitude":41.31,"longitude":69.28}},
             "offers":[{"min":100000,"currency":"UZS"}],
             "schemaOrg":{"@type":"Event","location":{"@type":"Place","name":"Дворец творчества молодёжи",
               "address":{"@type":"PostalAddress","addressLocality":"Ташкент","streetAddress":"ул. Мустакиллик, 2"}}},
             "mapping":{"schema":"wow2.event.v1","norm":"norm@1","rules":"rules@4","parser":"schema.org.event"}}
            """);

        facts.Starts.Should().Be(new EventWhen { Value = "2026-11-21T17:00", Precision = DatePrecision.Minute, Certainty = DateCertainty.Announced });
        facts.Organizer.Should().BeNull();
        facts.Place.Should().BeEquivalentTo(new EventPlace
        {
            Venue = "Дворец творчества молодёжи", Address = "ул. Мустакиллик, 2", City = "Ташкент", Latitude = 41.31, Longitude = 69.28,
        }, "the city comes from schema.org, and no country is invented: the source's DefaultCountry adds UZ later");
        facts.Images.Should().BeEmpty("sourced media stays evidence (events-integration #50)");
    }

    /// <summary>Afisha's reader puts the city only in schema.org's addressLocality; SCFD's mentions carry the venue and
    /// the street (events-dev, 2026-11-10: the published event lost its city).</summary>
    [Fact]
    public void TryMap_ShouldTakeTheCity_FromSchemaOrgsLocation_WhenNoMentionNamesIt()
    {
        var facts = Map("""
            {"schema":"wow2.event.v1","title":"Показ мод The Fashion Show Line 2026",
             "place":{"mentions":[{"kind":"venue","text":"Дворец творчества молодёжи"},{"kind":"address","text":"ул. Мустакиллик, 2"}]},
             "schemaOrg":{"@type":"Event","location":{"@type":"Place","name":"Дворец творчества молодёжи",
               "address":{"@type":"PostalAddress","addressLocality":"Ташкент","streetAddress":"ул. Мустакиллик, 2"},
               "geo":{"latitude":41.31,"longitude":69.28}}}}
            """);

        facts.Place.Should().BeEquivalentTo(new EventPlace
        {
            Venue = "Дворец творчества молодёжи", Address = "ул. Мустакиллик, 2", City = "Ташкент", Latitude = 41.31, Longitude = 69.28,
        });
    }

    [Theory]
    [InlineData("""{"value":"2026","precision":"year"}""", "2026", DatePrecision.Year)]
    [InlineData("""{"value":"2026-10-15","precision":"day"}""", "2026-10-15", DatePrecision.Day)]
    [InlineData("""{"value":"2026-10-15T09","precision":"hour"}""", "2026-10-15T09:00", DatePrecision.Minute)]
    public void TryMap_ShouldReadStarts(string starts, string value, DatePrecision precision)
    {
        var facts = Map($$"""{"schema":"wow2.event.v1","title":"Show","starts":{{starts}}}""");

        facts.Starts.Value.Should().Be(value);
        facts.Starts.Precision.Should().Be(precision);
    }

    [Theory]
    [InlineData("""{"schema":"wow2.event.v1"}""", "no title")]
    [InlineData("""{"schema":"wow2.event.v1","title":"   "}""", "no title")]
    [InlineData("""{"schema":"wow2.event.v1","title":"Show","starts":{"value":"--03-14","precision":"day"}}""", "unreadable dates: starts '--03-14' at precision 'day'")]
    [InlineData("""{"schema":"wow2.event.v1","title":"Show","starts":{"value":"2026-10","precision":"day"}}""", "unreadable dates: starts '2026-10' at precision 'day'")]
    public void TryMap_ShouldRefuseAMalformedPayload(string json, string reason)
    {
        ScfdEventMapper.TryMap(Json(json), Kinds, out _, out var why).Should().BeFalse();

        why.Should().Be(reason);
    }

    [Fact]
    public void TryMap_ShouldKeepSourcedMediaAndText_AsEvidenceOnly()
    {
        var facts = Map("""
            {"schema":"wow2.event.v1","title":"Show","description":"Long sourced text",
             "media":[{"kind":"image","url":"https://cdn.example/a.jpg"}],
             "sourceTags":["#fashion"]}
            """);

        facts.Images.Should().BeEmpty();
        facts.Summary.Should().BeEmpty();
        facts.Tags.Should().BeEmpty();
    }

    [Fact]
    public void TryMap_ShouldTitleInTheUnknownLocale_AndFallBackToTheOtherKind()
    {
        var unknown = Map("""{"schema":"wow2.event.v1","title":"Show","language":{"code":"other"},"kind":"concert"}""");
        var known = Map("""{"schema":"wow2.event.v1","title":"Shou","language":{"code":"uz","script":"Latn"},"kind":"Runway"}""");

        unknown.Title.Should().Equal(new Dictionary<string, string> { ["und"] = "Show" });
        unknown.Kind.Should().Be("other");
        unknown.Languages.Should().BeEmpty();
        known.Title.Keys.Should().Equal("uz-Latn");
        known.Kind.Should().Be("runway");
        known.Languages.Should().Equal("uz-Latn");
    }

    [Fact]
    public void TryMap_ShouldReadPlaceOrganizerOffersAndLinks()
    {
        var facts = Map("""
            {"schema":"wow2.event.v1","title":"Show",
             "organizer":{"name":" Mstar ","handle":"mstar","url":"https://t.me/mstar"},
             "place":{"mentions":[{"text":"Tashkent","kind":0},{"text":"Main road 1","kind":"street"},{"text":"Hall","kind":4,"geo":{"latitude":41.3,"longitude":69.2}}]},
             "offers":[{"amount":150000,"currency":"uzs","role":1},{"min":10,"max":5},{"free":true,"role":"listed"}],
             "contentLinks":[{"url":"https://tickets.example/1","kind":0},{"url":"https://mstar.uz","kind":"site"}],
             "schemaOrg":{"eventStatus":"https://schema.org/EventPostponed","eventAttendanceMode":"https://schema.org/MixedEventAttendanceMode",
                          "location":{"address":{"addressCountry":"uz"}}}}
            """);

        facts.Organizer.Should().Be(new EventOrganizer { Name = "Mstar", Website = "https://t.me/mstar" });
        facts.Place.Should().Be(new EventPlace { City = "Tashkent", Address = "Main road 1", Venue = "Hall", Country = "UZ", Latitude = 41.3, Longitude = 69.2 });
        facts.Offers.Should().HaveCount(2);
        facts.Offers[0].Should().Be(new EventOffer { Kind = OfferKind.Ticket, MinPrice = 150000, MaxPrice = 150000, Currency = "UZS" });
        facts.Offers[1].Should().Be(new EventOffer { Kind = OfferKind.Other, Free = true });
        facts.TicketUrl.Should().Be("https://tickets.example/1");
        facts.Website.Should().Be("https://mstar.uz");
        facts.Status.Should().Be(EventStatus.Postponed);
        facts.Attendance.Should().Be(EventAttendance.Mixed);
    }

    [Fact]
    public void MappingVersion_ShouldJoinTheStamp_WithinSixtyFourCharacters()
    {
        ScfdEventMapper.MappingVersion(Json("""{"mapping":{"schema":"wow2.event.v1","norm":"norm@2","rules":"rules@4","parser":null}}"""))
            .Should().Be("wow2.event.v1/norm@2/rules@4");
        ScfdEventMapper.MappingVersion(Json("{\"mapping\":{\"schema\":\"" + new string('x', 80) + "\"}}")).Should().HaveLength(64);
        ScfdEventMapper.MappingVersion(Json("""{"title":"No stamp"}""")).Should().BeNull();
        ScfdEventMapper.MappingVersion(null).Should().BeNull();
    }

    [Fact]
    public void Links_ShouldReadTheEvidence_AndAddTheCaptureOnce()
    {
        var links = ScfdEventMapper.Links(Json("""
            {"links":[{"url":"https://a.example/1","sourceId":"s-1","captureId":"c-1","capturedAtUtc":"2026-11-09T10:00:00+05:00","sourceFields":{"price":"10"}}],
             "capture":{"captureId":"c-2","capturedAtUtc":"2026-11-09T11:00:00Z","url":"https://a.example/1"}}
            """));
        var sameCapture = ScfdEventMapper.Links(Json("""
            {"links":[{"url":"https://a.example/1","captureId":"c-1"}],"capture":{"captureId":"c-1","url":"https://a.example/1"}}
            """));

        links.Should().HaveCount(2);
        links[0].SourceId.Should().Be("s-1");
        links[0].CapturedAtUtc.Should().Be(new DateTimeOffset(2026, 11, 9, 5, 0, 0, TimeSpan.Zero));
        links[0].SourceFields!.Value.GetProperty("price").GetString().Should().Be("10");
        links[1].CaptureId.Should().Be("c-2");
        sameCapture.Should().ContainSingle();
    }

    [Fact]
    public void PostIdAndRevision_ShouldReadThePayload()
    {
        var post = Guid.NewGuid();
        var data = Json($$"""{"postId":"{{post}}","revision":7}""");

        ScfdEventMapper.PostId(data).Should().Be(post);
        ScfdEventMapper.Revision(data).Should().Be(7);
        ScfdEventMapper.PostId(Json("\"text\"")).Should().BeNull();
    }

    private static EventFacts Map(string json)
    {
        ScfdEventMapper.TryMap(Json(json), Kinds, out var facts, out var reason).Should().BeTrue(reason);
        return facts;
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
