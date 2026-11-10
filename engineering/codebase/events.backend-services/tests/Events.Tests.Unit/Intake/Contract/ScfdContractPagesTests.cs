using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Events.Tests.Contract;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Unit.Intake.Contract;

/// <summary>The contract test with SCFD (the services sweep, after Events' own fixture diverged from the live record
/// twice): harvest's golden <c>wow2.event.v1</c> pages, copied unchanged, read through the real feed and mapper. Harvest
/// fails its own build on the first byte its host serves differently, so a pass here is a pass against the wire.</summary>
public sealed partial class ScfdContractPagesTests
{
    private static readonly string[] Kinds = ["runway", "expo"];

    private static readonly string[] KnownTypes =
        ["post.new", "post.changed", "post.extracted", "post.relisted", "post.unmerged", "post.merged", "post.purged", "link.state", "pipeline.run", "pipeline.health"];

    [Fact]
    public void Manifest_ShouldMatchEveryCopiedFile_ByteForByte()
    {
        var manifest = ContractPages.Manifest();

        manifest.Schema.Should().Be(EventsConstants.EventSchema);
        foreach (var file in manifest.Files)
            Convert.ToHexStringLower(SHA256.HashData(ContractPages.Bytes(file.Name))).Should().Be(file.Sha256, $"{file.Name} is copied unchanged");
        Directory.GetFiles(Path.Combine(ContractPages.Folder, "wow2.event.v1")).Select(Path.GetFileName)
            .Should().BeEquivalentTo([.. manifest.Files.Select(file => file.Name), ContractPages.ManifestName], "the folder holds what the manifest names, nothing else");
    }

    [Fact]
    public void Source_ShouldNameTheHarvestTagAndCommitTheFolderCameFrom()
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(ContractPages.Folder, "source.json")));

        source.RootElement.GetProperty("repository").GetString().Should().Be("wow-two-sdk-beta/wow-two-sdk-beta.harvest");
        source.RootElement.GetProperty("tag").GetString().Should().MatchRegex(@"^v\d+\.\d+\.\d+$");
        source.RootElement.GetProperty("commit").GetString().Should().MatchRegex("^[0-9a-f]{40}$");
    }

    /// <summary>Every page reads through the real feed, record for record as the manifest names them; every record's type
    /// is one Events knows, and every post payload maps to facts.</summary>
    [Fact]
    public async Task EveryPage_ShouldReadThroughTheFeed_AndEveryPostPayloadMap()
    {
        foreach (var file in ContractPages.Pages())
        {
            var page = await ReadAsync(file.Name);

            page.Next.Should().NotBeNullOrEmpty($"{file.Name} carries the next cursor");
            page.Records.Should().HaveCount(file.Cases is ["empty"] ? 0 : file.Cases.Count, $"{file.Name} carries one record per case");
            foreach (var record in page.Records)
            {
                record.EventId.Should().NotBeNull();
                record.ItemId.Should().NotBeNullOrEmpty();
                record.SubscriptionId.Should().Be(Guid.Parse(ContractPages.SubscriptionId));
                record.OccurredAtUtc.Should().NotBeNull();
                KnownTypes.Should().Contain(record.Type, $"{file.Name} carries only types Events handles");
                if (record.Data is not { } data)
                    continue;

                ScfdEventMapper.Schema(data).Should().Be(EventsConstants.EventSchema);
                ScfdEventMapper.PostId(data).Should().Be(record.PostId);
                ScfdEventMapper.Revision(data).Should().Be(record.Revision);
                ScfdEventMapper.TryMap(data, Kinds, out _, out var reason).Should().BeTrue($"{file.Name} {record.Type}: {reason}");
            }
        }
    }

    [Fact]
    public async Task Subscription_ShouldReadAsIntakeChecksItBeforeAPull()
    {
        var (feed, _) = ContractPages.Feed();

        var subscription = await feed.ReadSubscriptionAsync(ContractPages.Source(), CancellationToken.None);

        subscription.Should().Be(new ScfdSubscription(FilterKey: null, Schema: EventsConstants.EventSchema, State: ScfdSubscription.ActiveState));
        subscription.MismatchWith(ContractPages.Source()).Should().BeNull("a source that names no filter takes an unfiltered subscription");
        subscription.MismatchWith(ContractPages.Source(named => named.Filter = "ocharo-fashion-v1")).Should().Be("filter-mismatch", "the golden subscription carries no filter");
    }

    /// <summary>An empty page returns the cursor it was sent, and Events keeps <c>page.Next ?? cursor</c>: the cursor never
    /// moves backwards or resets on a pull that finds nothing.</summary>
    [Fact]
    public async Task EmptyPage_ShouldReturnTheCursorItWasSent()
    {
        var changed = await ReadAsync("03-changed.json");
        var empty = await ReadAsync("04-empty.json");

        empty.Records.Should().BeEmpty();
        empty.HasMore.Should().BeFalse();
        empty.Next.Should().Be(changed.Next);
    }

    [Fact]
    public async Task NewPost_ShouldMapEveryFactEventsReads()
    {
        var record = (await ReadAsync("01-new.json")).Records.Single();
        var data = record.Data!.Value;

        record.Type.Should().Be("post.new");
        record.Reason.Should().Be("ingest");
        ScfdEventMapper.TryMap(data, Kinds, out var facts, out _).Should().BeTrue();
        facts.Title.Should().Equal(new Dictionary<string, string> { ["ru-Cyrl"] = "Вечер живого джаза" });
        facts.Kind.Should().Be("other", "the record names no kind");
        facts.Starts.Should().Be(new EventWhen { Value = "2026-12-05T19:00", Precision = DatePrecision.Minute, Certainty = DateCertainty.Announced });
        facts.Ends.Should().Be(new EventWhen { Value = "2026-12-06", Precision = DatePrecision.Day, Certainty = DateCertainty.Announced });
        facts.Organizer.Should().Be(new EventOrganizer { Name = "Tashkent Jazz Club", Website = "https://jazz.example.test/" });
        facts.Place.Should().Be(new EventPlace
        {
            Venue = "Дворец Истиклол", Address = "ул. Амира Темура, 107", City = "Ташкент", Latitude = 41.3139, Longitude = 69.2803,
        });
        facts.Offers.Should().ContainSingle().Which.Should().Be(new EventOffer { Kind = OfferKind.Ticket, MinPrice = 150000, MaxPrice = 150000, Currency = "UZS" });
        facts.Website.Should().Be("https://afisha.example.test/venues/istiqlol");
        facts.TicketUrl.Should().Be("https://afisha.example.test/events/jazz-night/tickets");
        facts.Languages.Should().Equal("ru-Cyrl");
        facts.Images.Should().BeEmpty("sourced media stays evidence (events-integration #50)");
        facts.Summary.Should().BeEmpty("sourced text stays evidence");

        ScfdEventMapper.MappingVersion(data).Should().Be("wow2.event.v1/norm@1/rules@4/schema.org.event");
        ScfdEventMapper.PostState(data).Should().Be("live");
        ScfdEventMapper.MergedInto(data).Should().BeNull();
        var link = ScfdEventMapper.Links(data).Should().ContainSingle("the capture is the link's own").Subject;
        link.Url.Should().Be("https://afisha.example.test/events/jazz-night");
        link.SourceId.Should().Be("00000000-0000-7000-8000-000000000007");
        link.CaptureId.Should().Be("00000000-0000-7000-8000-000000000008");
        link.CapturedAtUtc.Should().Be(new DateTimeOffset(2026, 11, 10, 9, 2, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ChangedPost_ShouldCarryTheSamePostAtTheNextRevision()
    {
        var first = (await ReadAsync("01-new.json")).Records.Single();
        var changed = (await ReadAsync("03-changed.json")).Records.Single();

        changed.Type.Should().Be("post.changed");
        changed.PostId.Should().Be(first.PostId);
        changed.Revision.Should().Be(first.Revision + 1);
        changed.EventId.Should().NotBe(first.EventId!.Value, "each delivery has its own id, the key Events dedups on");
    }

    /// <summary>The lifecycle page: a listing gone and back arrive as <c>link.state</c> at new revisions with the post's
    /// state; a merge names the survivor on the loser's record; an unmerge returns both posts with no <c>mergedInto</c>.</summary>
    [Fact]
    public async Task Lifecycle_ShouldCarryThePostsStateAndItsMerge()
    {
        var page = await ReadAsync("05-lifecycle.json");
        var original = (await ReadAsync("01-new.json")).Records.Single().PostId;
        var copy = page.Records[0].PostId;

        (string? Type, Guid? Post, int? Revision, string? State, Guid? MergedInto)[] expected =
        [
            ("post.new", copy, 1, "live", null),
            ("link.state", original, 3, "gone", null),
            ("link.state", original, 4, "live", null),
            ("post.merged", copy, 2, "live", original),
            ("post.changed", original, 5, "live", null),
            ("post.unmerged", original, 6, "live", null),
            ("post.unmerged", copy, 3, "live", null),
        ];
        page.Records.Select(record => (record.Type, Post: record.PostId, record.Revision, State: ScfdEventMapper.PostState(record.Data), MergedInto: ScfdEventMapper.MergedInto(record.Data)))
            .Should().Equal(expected);
        Title(page.Records[4]).Should().NotBe(Title(page.Records[5]), "the unmerge restores the survivor's own content, which Events applies as a revision");
        Title(page.Records[5]).Should().Be("Вечер живого джаза");
    }

    /// <summary>The classification object harvest defines (<c>PostClassification</c>) is what Events' <c>payload</c>
    /// classifier reads: relevant publishes with its kind, irrelevant holds, uncertain or under the source's minimum
    /// confidence decides nothing.</summary>
    [Fact]
    public async Task Classification_ShouldDecideAsThePayloadClassifierReadsIt()
    {
        var source = ContractPages.Source(payload =>
        {
            payload.Review = EventsIntakeSourceOptions.AutoReview;
            payload.Classifier = EventsIntakeSourceOptions.PayloadClassifier;
            payload.MinConfidence = 0.5;
        });

        var verdicts = (await ReadAsync("06-classification.json")).Records.Select(record => IntakeVerdict.Of(source, record.Data!.Value)).ToList();

        (bool? Relevant, string? Kind)[] expected = [(true, "runway"), (false, null), (null, null), (null, "runway")];
        verdicts.Select(verdict => (verdict.Relevant, verdict.Kind)).Should().Equal(expected);
        verdicts.Should().OnlyContain(verdict => verdict.Actor == "classifier:model:contract@1");
        verdicts[0].Reason.Should().Be("relevant (0.9): A designer collection shown on a runway.");
    }

    [Fact]
    public async Task Classification_ShouldGiveTheEventItsKind_WhenTheCatalogHasIt()
    {
        var records = (await ReadAsync("06-classification.json")).Records;

        ScfdEventMapper.TryMap(records[0].Data!.Value, Kinds, out var runway, out _).Should().BeTrue();
        ScfdEventMapper.TryMap(records[1].Data!.Value, Kinds, out var none, out _).Should().BeTrue();

        runway.Kind.Should().Be("runway", "the top-level kind is the classification's");
        none.Kind.Should().Be("other");
    }

    /// <summary>The live Afisha place through harvest's real extractor, before and after its city fix: before, no city
    /// exists in the record and none is invented; after, the city mention gives it.</summary>
    [Fact]
    public async Task AfishaPlace_ShouldMapBeforeAndAfterTheCityFix()
    {
        var records = (await ReadAsync("07-afisha-place.json")).Records;

        ScfdEventMapper.TryMap(records[0].Data!.Value, Kinds, out var before, out _).Should().BeTrue();
        ScfdEventMapper.TryMap(records[1].Data!.Value, Kinds, out var after, out _).Should().BeTrue();

        before.Place.Should().Be(new EventPlace
        {
            Venue = "Дворец творчества молодёжи", Address = "Ташкент, просп. Мустакиллик, 2", Latitude = 41.3153, Longitude = 69.2727,
        });
        after.Place.Should().Be(new EventPlace
        {
            Venue = "Дворец творчества молодёжи", Address = "просп. Мустакиллик, 2", City = "Ташкент", Latitude = 41.3153, Longitude = 69.2727,
        });
    }

    [Fact]
    public async Task Purge_ShouldNameThePostAndCarryNoPayload()
    {
        var first = (await ReadAsync("01-new.json")).Records.Single();
        var purge = (await ReadAsync("08-purged.json")).Records.Single();

        purge.Type.Should().Be("post.purged");
        purge.PostId.Should().Be(first.PostId);
        purge.Revision.Should().Be(7);
        purge.Data.Should().BeNull();
    }

    /// <summary>Cursors are opaque to Events: it sends back exactly what SCFD served, URL-escaped.</summary>
    [Fact]
    public async Task Pull_ShouldSendTheCursorExactlyAsServed()
    {
        var (feed, scfd) = ContractPages.Feed("01-new.json", "03-changed.json");
        var first = await feed.ReadAsync(ContractPages.Source(), cursor: null, limit: 50, CancellationToken.None);

        await feed.ReadAsync(ContractPages.Source(), first.Next, limit: 50, CancellationToken.None);

        scfd.Cursors.Should().Equal(null, first.Next);
        CursorPattern().IsMatch(first.Next!).Should().BeTrue("harvest keeps a cursor as base64");
    }

    private static async Task<ObservationFeedPage> ReadAsync(string page)
    {
        var (feed, _) = ContractPages.Feed(page);
        return await feed.ReadAsync(ContractPages.Source(), cursor: null, limit: 50, CancellationToken.None);
    }

    private static string? Title(ScfdDeliveryRecord record) => record.Data!.Value.GetProperty("title").GetString();

    [GeneratedRegex("^[A-Za-z0-9+/]+={0,2}$")]
    private static partial Regex CursorPattern();
}
