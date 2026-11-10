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

    /// <summary>The live Afisha place on dev before SCFD's city fix (parser afisha-html@3, sent by the scfd lane on
    /// 2026-11-10): the mentions name the venue and the whole address, schema.org's address is one plain string, and
    /// nothing names a city or a country. The mentions keep <c>geo: null</c>; schema.org's coordinates are strings.</summary>
    internal const string LivePlaceBefore = """
        "place":{"mentions":[{"text":"Дворец творчества молодёжи","kind":"venue","geo":null},{"text":"Ташкент, просп. Мустакиллик, 2","kind":"address","geo":null}],"geo":{"latitude":41.3153,"longitude":69.2727}},
        "schemaOrg":{"@type":"Event","location":{"@type":"Place","name":"Дворец творчества молодёжи","address":"Ташкент, просп. Мустакиллик, 2","geo":{"@type":"GeoCoordinates","latitude":"41.3153000","longitude":"69.2727000"}}}
        """;

    /// <summary>The same place after SCFD's fix (parser afisha-html@4, from the adapter's code and tests, not yet seen on
    /// dev): city and street mentions replace the address mention, and schema.org's address becomes a PostalAddress.</summary>
    internal const string LivePlaceAfter = """
        "place":{"mentions":[{"text":"Дворец творчества молодёжи","kind":"venue","geo":null},{"text":"Ташкент","kind":"city","geo":null},{"text":"просп. Мустакиллик, 2","kind":"street","geo":null}],"geo":{"latitude":41.3153,"longitude":69.2727}},
        "schemaOrg":{"@type":"Event","location":{"@type":"Place","name":"Дворец творчества молодёжи","address":{"@type":"PostalAddress","addressLocality":"Ташкент","streetAddress":"просп. Мустакиллик, 2"},"geo":{"@type":"GeoCoordinates","latitude":"41.3153000","longitude":"69.2727000"}}}
        """;

    /// <summary>The live record before the fix stores no city, and the address is never split to find one; no country
    /// is invented either: the source's <c>DefaultCountry</c> adds UZ later.</summary>
    [Fact]
    public void TryMap_ShouldReadTheLiveAfishaPlace_BeforeTheCityFix()
    {
        var facts = Map(LiveRecord(LivePlaceBefore));

        facts.Starts.Should().Be(new EventWhen { Value = "2026-11-21T17:00", Precision = DatePrecision.Minute, Certainty = DateCertainty.Announced });
        facts.Organizer.Should().BeNull();
        facts.Place.Should().BeEquivalentTo(new EventPlace
        {
            Venue = "Дворец творчества молодёжи", Address = "Ташкент, просп. Мустакиллик, 2", Latitude = 41.3153, Longitude = 69.2727,
        });
        facts.Images.Should().BeEmpty("sourced media stays evidence (events-integration #50)");
    }

    [Fact]
    public void TryMap_ShouldReadTheLiveAfishaPlace_AfterTheCityFix()
    {
        var facts = Map(LiveRecord(LivePlaceAfter));

        facts.Place.Should().BeEquivalentTo(new EventPlace
        {
            Venue = "Дворец творчества молодёжи", Address = "просп. Мустакиллик, 2", City = "Ташкент", Latitude = 41.3153, Longitude = 69.2727,
        });
    }

    /// <summary>SCFD's older wire wrote a mention's kind as LocationKind's number (City 0, Street 3, Venue 4, Address 5).</summary>
    [Fact]
    public void TryMap_ShouldReadMentionKinds_AsNumbers()
    {
        var facts = Map("""
            {"schema":"wow2.event.v1","title":"Show",
             "place":{"mentions":[{"kind":4,"text":"Venue"},{"kind":0,"text":"Ташкент"},{"kind":3,"text":"Street 2"},{"kind":5,"text":"Whole address"}]}}
            """);

        facts.Place.Should().BeEquivalentTo(new EventPlace { Venue = "Venue", Address = "Whole address", City = "Ташкент" });
    }

    /// <summary>A source's own schema.org location fills what no mention names: the city from addressLocality, and its
    /// coordinates, which schema.org may write as text.</summary>
    [Fact]
    public void TryMap_ShouldFillTheCityAndCoordinates_FromSchemaOrgsLocation()
    {
        var facts = Map("""
            {"schema":"wow2.event.v1","title":"Show",
             "place":{"mentions":[{"kind":"venue","text":"Дворец творчества молодёжи"}]},
             "schemaOrg":{"@type":"Event","location":{"@type":"Place","name":"Дворец творчества молодёжи",
               "address":{"@type":"PostalAddress","addressLocality":"Ташкент","streetAddress":"просп. Мустакиллик, 2"},
               "geo":{"@type":"GeoCoordinates","latitude":"41.3153000","longitude":"69.2727000"}}}}
            """);

        facts.Place.Should().BeEquivalentTo(new EventPlace
        {
            Venue = "Дворец творчества молодёжи", Address = "просп. Мустакиллик, 2", City = "Ташкент", Latitude = 41.3153, Longitude = 69.2727,
        });
    }

    private static string LiveRecord(string place) => $$$"""
        {"schema":"wow2.event.v1","title":"Показ мод The Fashion Show Line 2026",
         "starts":{"value":"2026-11-21T17:00+05:00","precision":"second"},
         "postedAt":{"value":"2026-10-14","precision":"day"},
         "organizer":null,
         "offers":[{"min":100000,"currency":"UZS"}],
         {{{place}}},
         "mapping":{"schema":"wow2.event.v1","norm":"norm@1","rules":"rules@4","parser":"afisha-html@3"}}
        """;

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
