using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events;
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
            Venue = "Дворец творчества молодёжи", Address = "просп. Мустакиллик, 2", City = "Tashkent", Latitude = 41.3153, Longitude = 69.2727,
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

        facts.Place.Should().BeEquivalentTo(new EventPlace { Venue = "Venue", Address = "Whole address", City = "Tashkent" });
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
            Venue = "Дворец творчества молодёжи", Address = "просп. Мустакиллик, 2", City = "Tashkent", Latitude = 41.3153, Longitude = 69.2727,
        });
    }

    /// <summary>Afisha writes <c>Ташкент</c> where the catalog's own events say <c>Tashkent</c> (events-dev, 2026-11-16): a
    /// known city takes one Latin spelling, whichever script or apostrophe the source used, so Ocharo's city filter and
    /// grouping never split one city in two. The same city from schema.org's <c>addressLocality</c> maps alike.</summary>
    [Theory]
    [InlineData("Ташкент", "Tashkent")]
    [InlineData("Самарканд", "Samarkand")]
    [InlineData("Бухара", "Bukhara")]
    [InlineData("Toshkent", "Tashkent")]
    [InlineData("  ТАШКЕНТ ", "Tashkent")]
    [InlineData("Qo‘qon", "Kokand")]
    [InlineData("Farg'ona", "Fergana")]
    [InlineData("Tashkent", "Tashkent")]
    public void TryMap_ShouldNameAKnownCityInOneLatinSpelling(string written, string city)
    {
        var fromMention = Map("""{"schema":"wow2.event.v1","title":"Show","place":{"mentions":[{"kind":"city","text":"CITY"}]}}""".Replace("CITY", written));
        var fromSchemaOrg = Map("""
            {"schema":"wow2.event.v1","title":"Show",
             "schemaOrg":{"location":{"address":{"addressLocality":"CITY"}}}}
            """.Replace("CITY", written));

        fromMention.Place!.City.Should().Be(city);
        fromSchemaOrg.Place!.City.Should().Be(city);
    }

    [Fact]
    public void TryMap_ShouldPassAnUnknownCityThrough_AsTheSourceWroteIt()
    {
        var facts = Map("""{"schema":"wow2.event.v1","title":"Show","place":{"mentions":[{"kind":"city","text":"Париж"}]}}""");

        facts.Place!.City.Should().Be("Париж");
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

    /// <summary>SCFD's <c>certainty</c> (S17): <c>estimated</c> is an AI-estimated date and reads as an estimate at the
    /// precision it states; <c>stated</c>, none, or a value Events does not know reads as announced, as before.</summary>
    [Theory]
    [InlineData("""{"value":"2026-12","precision":"month","certainty":"estimated"}""", "2026-12", DatePrecision.Month, DateCertainty.Estimated)]
    [InlineData("""{"value":"2026-12-12","precision":"day","certainty":"estimated"}""", "2026-12-12", DatePrecision.Day, DateCertainty.Estimated)]
    [InlineData("""{"value":"2026","precision":"year","certainty":" Estimated "}""", "2026", DatePrecision.Year, DateCertainty.Estimated)]
    [InlineData("""{"value":"2026-12-12","precision":"day","certainty":"stated"}""", "2026-12-12", DatePrecision.Day, DateCertainty.Announced)]
    [InlineData("""{"value":"2026-12-12T19:00:00+05:00","precision":"second","certainty":"stated"}""", "2026-12-12T19:00", DatePrecision.Minute, DateCertainty.Announced)]
    [InlineData("""{"value":"2026-12-12","precision":"day"}""", "2026-12-12", DatePrecision.Day, DateCertainty.Announced)]
    [InlineData("""{"value":"2026-12-12","precision":"day","certainty":"confirmed"}""", "2026-12-12", DatePrecision.Day, DateCertainty.Announced)]
    [InlineData("""{"value":"2026-12-12","precision":"day","certainty":null}""", "2026-12-12", DatePrecision.Day, DateCertainty.Announced)]
    public void TryMap_ShouldReadTheDatesCertainty_AndNeverCallAnEstimateAnnounced(string starts, string value, DatePrecision precision, DateCertainty certainty)
    {
        var facts = Map($$"""{"schema":"wow2.event.v1","title":"Show","starts":{{starts}},"ends":{{starts}}}""");

        var expected = new EventWhen { Value = value, Precision = precision, Certainty = certainty };
        facts.Starts.Should().Be(expected);
        facts.Ends.Should().Be(expected);
    }

    [Fact]
    public void TryMap_ShouldKeepAnUndatedPostUnknown_WhateverItsCertainty()
    {
        var facts = Map("""{"schema":"wow2.event.v1","title":"Show","starts":null,"ends":null}""");

        facts.Starts.Should().Be(new EventWhen { Value = null, Precision = DatePrecision.Unknown, Certainty = DateCertainty.Announced });
        facts.Ends.Should().BeNull();
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

    /// <summary>OE06: SCFD's machine English title sits beside the source's own title, never over a title the source states
    /// in that language; a title not marked machine, or with no text or language, is left out.</summary>
    [Fact]
    public void TryMap_ShouldPutTheMachineEnglishTitle_BesideTheSourceTitle()
    {
        var russian = Map("""
            {"schema":"wow2.event.v1","title":"Выставка","language":{"code":"ru","script":"Cyrl"},
             "titles":[{"text":" Exhibition ","language":"en","machine":true},{"text":"Ausstellung","language":"de"},{"text":"","language":"fr","machine":true}]}
            """);
        var english = Map("""
            {"schema":"wow2.event.v1","title":"Fashion Week","language":{"code":"en"},"titles":[{"text":"Fashion week (machine)","language":"en","machine":true}]}
            """);
        var unknown = Map("""{"schema":"wow2.event.v1","title":"Show","titles":[{"text":"Show","language":"EN","machine":true}]}""");

        russian.Title.Should().Equal(new Dictionary<string, string> { ["ru-Cyrl"] = "Выставка", ["en"] = "Exhibition" });
        russian.Languages.Should().Equal(["ru-Cyrl"], "the languages are the source's own, never the machine's");
        english.Title.Should().Equal(new Dictionary<string, string> { ["en"] = "Fashion Week" }, "the source's own English words win");
        unknown.Title.Should().Equal(new Dictionary<string, string> { ["und"] = "Show", ["en"] = "Show" });
    }

    /// <summary>OE06: the observation records which titles a machine wrote, on the link the title came from
    /// (<c>fieldSources.title</c>), keeping that link's own fields.</summary>
    [Fact]
    public void Links_ShouldRecordTheMachineTitles_OnTheTitlesSourceLink()
    {
        var links = ScfdEventMapper.Links(Json("""
            {"titles":[{"text":"Exhibition","language":"en","machine":true}],"fieldSources":{"title":"l-2"},
             "links":[{"id":"l-1","url":"https://a.example/1"},{"id":"l-2","url":"https://a.example/2","sourceFields":{"price":"10"}}]}
            """));
        var none = ScfdEventMapper.Links(Json("""{"links":[{"id":"l-1","url":"https://a.example/1"}]}"""));

        links[0].SourceFields.Should().BeNull();
        links[1].SourceFields!.Value.GetProperty("price").GetString().Should().Be("10");
        links[1].SourceFields!.Value.GetProperty("machineTitles").EnumerateArray().Select(item => item.GetString()).Should().Equal("en");
        none.Single().SourceFields.Should().BeNull("a payload without machine titles adds nothing");
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

    /// <summary>SCFD marks every in-page link <c>site</c> (events-dev, 2026-11-16): the aggregator's own pages, a social
    /// profile and a map pin read as the event's website. Only a link on a host of its own is one.</summary>
    [Theory]
    [InlineData("https://afisha.example.test/venues/istiqlol")]
    [InlineData("https://www.afisha.example.test/venues/istiqlol")]
    [InlineData("https://en.afisha.example.test/venues/istiqlol")]
    [InlineData("https://t.me/mstar")]
    [InlineData("https://www.instagram.com/mstar/")]
    [InlineData("https://vk.com/mstar")]
    [InlineData("https://maps.google.com/?q=41.3,69.2")]
    [InlineData("https://www.google.com/maps/place/Humo+Arena")]
    [InlineData("https://yandex.uz/maps/10335/tashkent/")]
    [InlineData("https://2gis.uz/tashkent/firm/1")]
    public void TryMap_ShouldTakeNoWebsite_FromAnAggregatorSocialOrMapLink(string link)
    {
        var facts = Map(WithSiteLinks(link));

        facts.Website.Should().BeNull();
    }

    [Fact]
    public void TryMap_ShouldKeepAnExternalOrganizerSite_AndSkipTheLinksBeforeIt()
    {
        var facts = Map(WithSiteLinks("https://afisha.example.test/venues/istiqlol", "https://t.me/mstar", "https://mstar.example.uz/expo", "https://other.example.uz"));

        facts.Website.Should().Be("https://mstar.example.uz/expo");
    }

    [Fact]
    public void TryMap_ShouldKeepTheOnlySiteLink_WhenThePayloadNamesNoSourceLink()
    {
        var facts = Map("""{"schema":"wow2.event.v1","title":"Show","contentLinks":[{"url":"https://mstar.example.uz","kind":"site"}]}""");

        facts.Website.Should().Be("https://mstar.example.uz");
    }

    /// <summary>A payload whose post links sit on the aggregator, with one <c>site</c> content link per given URL.</summary>
    private static string WithSiteLinks(params string[] urls) =>
        """{"schema":"wow2.event.v1","title":"Show","links":[{"url":"https://afisha.example.test/events/jazz-night","role":"primary"}],"contentLinks":["""
        + string.Join(',', urls.Select(url => "{\"url\":\"" + url + "\",\"kind\":\"site\"}")) + "]}";

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

    /// <summary>Stated deadlines map at their own precision (E08); one whose date is unreadable is left out rather than
    /// failing the record, and a kind Events does not know reads as <c>other</c>.</summary>
    [Fact]
    public void TryMap_ShouldReadTheStatedDeadlines_AndLeaveOutAnUnreadableOne()
    {
        var facts = Map("""
            {"schema":"wow2.event.v1","title":"Fair","deadlines":[
              {"kind":"application","value":"2026-10-26","precision":"day","raw":"26 October","status":"open"},
              {"kind":"application","value":"soon","precision":"day"},
              {"kind":"stand-booking","value":"2026-11","precision":"month"},
              {"kind":"application","value":null,"precision":"day"}]}
            """);

        facts.Deadlines.Should().Equal(
            new EventDeadline { Kind = DeadlineKind.Application, At = new EventWhen { Value = "2026-10-26", Precision = DatePrecision.Day } },
            new EventDeadline { Kind = DeadlineKind.Other, At = new EventWhen { Value = "2026-11", Precision = DatePrecision.Month } });
        Map("""{"schema":"wow2.event.v1","title":"Fair","deadlines":[]}""").Deadlines.Should().BeEmpty();
        Map("""{"schema":"wow2.event.v1","title":"Fair"}""").Deadlines.Should().BeEmpty("a payload from before harvest E07 names none");
    }

    [Fact]
    public void TryMap_ShouldKeepAtMostTheListLimitOfDeadlines()
    {
        var many = string.Join(',', Enumerable.Range(1, EventsConstants.MaxListLength + 10).Select(day => $$"""{"kind":"application","value":"2026-10-{{day % 28 + 1:00}}","precision":"day"}"""));

        Map($$"""{"schema":"wow2.event.v1","title":"Fair","deadlines":[{{many}}]}""").Deadlines.Should().HaveCount(EventsConstants.MaxListLength);
    }

    /// <summary>A zone is set only when the start states an offset and that offset is the zone's own on that date.</summary>
    [Theory]
    [InlineData("2026-12-05T19:00:00+05:00", "Asia/Tashkent", "Asia/Tashkent")]
    [InlineData("2026-12-05T19:00+05:00", "Asia/Tashkent", "Asia/Tashkent")]
    [InlineData("2026-12-05T14:00:00Z", "Asia/Tashkent", null)]
    [InlineData("2026-12-05T19:00:00", "Asia/Tashkent", null)]
    [InlineData("2026-12-05", "Asia/Tashkent", null)]
    [InlineData("2026-07-05T19:00:00+02:00", "Europe/Berlin", "Europe/Berlin")]
    [InlineData("2026-12-05T19:00:00+02:00", "Europe/Berlin", null)]
    [InlineData("2026-12-05T19:00:00+05:00", null, null)]
    [InlineData("2026-12-05T19:00:00+05:00", "Not/AZone", null)]
    public void TimeZone_ShouldBeTheSourcesZone_OnlyWhenTheStartsOffsetIsItsOwnOnThatDate(string starts, string? zone, string? expected)
    {
        var data = Json($$$"""{"schema":"wow2.event.v1","title":"Show","starts":{"value":"{{{starts}}}","precision":"minute"}}""");

        ScfdEventMapper.TimeZone(data, zone).Should().Be(expected);
    }

    /// <summary>The post's state reads as an event source's state (E15), as a label or SCFD's number; a relisted link is live.</summary>
    [Theory]
    [InlineData("\"live\"", EventSourceState.Live)]
    [InlineData("\"relisted\"", EventSourceState.Live)]
    [InlineData("\"Suspect\"", EventSourceState.Suspect)]
    [InlineData("\"gone\"", EventSourceState.Gone)]
    [InlineData("2", EventSourceState.Gone)]
    [InlineData("\"archived\"", null)]
    [InlineData("null", null)]
    public void SourceState_ShouldReadThePostsState(string state, EventSourceState? expected)
    {
        ScfdEventMapper.SourceState(Json($$"""{"state":{{state}}}""")).Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"postedAt":{"value":"2026-11-08T09:00:00.0000000+00:00","precision":"second","timeZone":"UTC"}}""", "2026-11-08T09:00:00+00:00")]
    [InlineData("""{"postedAt":{"value":"2026-11-08T09:00:00+05:00","precision":"second"}}""", "2026-11-08T04:00:00+00:00")]
    [InlineData("""{"postedAt":{"value":"2026-11-08","precision":"day"}}""", "2026-11-08T00:00:00+00:00")]
    [InlineData("""{"postedAt":{"value":"2026-11","precision":"month"}}""", null)]
    [InlineData("""{"postedAt":{"value":"yesterday"}}""", null)]
    [InlineData("""{"postedAt":null}""", null)]
    [InlineData("""{}""", null)]
    public void PostedAt_ShouldReadTheSourcesPostedTimeInUtc(string json, string? expected)
    {
        ScfdEventMapper.PostedAt(Json(json)).Should().Be(expected is null ? null : DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
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
