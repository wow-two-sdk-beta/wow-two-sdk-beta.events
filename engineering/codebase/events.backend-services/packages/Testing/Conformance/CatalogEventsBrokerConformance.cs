using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="ICatalogEventsBroker"/>: the owner organization writes; a curator's entry publishes,
/// and so does a member's proposal in a private catalog, while in a shared or public one it waits as a candidate; readers
/// outside the owner see published events only; writes carry the revision they read.</summary>
public abstract class CatalogEventsBrokerConformance : EventsConformance
{
    /// <summary>A curator's entry publishes, at revision 1, in the catalog.</summary>
    [Fact]
    public async Task CreateAsync_ShouldPublishTheEvent_ACuratorEntered()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);

        var created = await CreateEventAsync(owner, catalog.Slug, Facts("Tashkent Fashion Week", "2026-10-15"));

        created.Catalog.Should().Be(catalog.Slug);
        created.Review.Should().Be(EventReviewState.Published);
        created.Revision.Should().Be(1);
        created.Facts.Title.Should().ContainKey(EventsConstants.UnknownLocale).WhoseValue.Should().Be("Tashkent Fashion Week");
        created.Facts.Starts.Value.Should().Be("2026-10-15");
        created.Facts.Starts.Precision.Should().Be(DatePrecision.Day);
    }

    /// <summary>A member's proposal in a private catalog publishes at once: the organization's event list and its
    /// tracking list show it.</summary>
    [Fact]
    public async Task ProposeAsync_ShouldPublishTheEvent_InAPrivateCatalog()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);

        var proposed = await ProposeEventAsync(owner, catalog.Slug, Facts("Member's private proposal", "2099-04-01"));

        proposed.Review.Should().Be(EventReviewState.Published);
        proposed.PublishedAtUtc.Should().NotBeNull();
        Value(await As(owner).Events.ListAsync(catalog.Slug, new EventQuery())).Items.Select(item => item.Id).Should().Equal(proposed.Id);
        Value(await As(NewMember(owner.OrganizationId)).Tracking.ListAsync(catalog.Slug, new TrackingQuery())).Items
            .Select(item => item.Event.Id).Should().Equal(proposed.Id);
    }

    /// <summary>A member's proposal in a shared or public catalog waits as a candidate the owner reads, and no list shows it.</summary>
    /// <param name="visibility">The catalog's visibility.</param>
    [Theory]
    [InlineData(CatalogVisibility.Shared)]
    [InlineData(CatalogVisibility.Public)]
    public async Task ProposeAsync_ShouldKeepTheEventACandidate_InASharedOrPublicCatalog(CatalogVisibility visibility)
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner, visibility);

        var proposed = await ProposeEventAsync(owner, catalog.Slug, Facts("Member's shared proposal", "2099-04-01"));

        proposed.Review.Should().Be(EventReviewState.Candidate);
        Value(await As(owner).Events.GetAsync(catalog.Slug, proposed.Id)).Review.Should().Be(EventReviewState.Candidate);
        Value(await As(owner).Tracking.ListAsync(catalog.Slug, new TrackingQuery())).Items.Should().BeEmpty();
    }

    /// <summary>Another organization proposes nothing into a public catalog it reads.</summary>
    [Fact]
    public async Task ProposeAsync_ShouldFailForbidden_ForAnotherOrganization()
    {
        var catalog = await CreateCatalogAsync(NewMember(), CatalogVisibility.Public);

        var proposed = await As(NewMember()).Events.ProposeAsync(catalog.Slug, new EventSpec { Facts = Facts() });

        Error(proposed).Type.Should().Be(AppErrorType.Forbidden);
    }

    /// <summary>The owner lists published events by default, and candidates when it asks for them.</summary>
    [Fact]
    public async Task ListAsync_ShouldListPublishedEvents_AndCandidatesOnlyWhenAsked()
    {
        var owner = NewMember();
        var catalog = await CreateReviewedCatalogAsync(owner);
        var published = await CreateEventAsync(owner, catalog.Slug);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);

        var listed = Value(await As(owner).Events.ListAsync(catalog.Slug, new EventQuery()));
        var candidates = Value(await As(owner).Events.ListAsync(catalog.Slug, new EventQuery { Review = EventReviewState.Candidate }));

        listed.Items.Select(item => item.Id).Should().Equal(published.Id);
        candidates.Items.Select(item => item.Id).Should().Equal(candidate.Id);
    }

    /// <summary>A page ends with a cursor to the next while more remain, and the last page with none.</summary>
    [Fact]
    public async Task ListAsync_ShouldPage_ByTheCursor()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var events = new List<EventModel>();
        foreach (var day in new[] { "2026-12-01", "2026-12-02", "2026-12-03" })
            events.Add(await CreateEventAsync(owner, catalog.Slug, Facts(starts: day)));

        var first = Value(await As(owner).Events.ListAsync(catalog.Slug, new EventQuery { Limit = 2 }));
        var second = Value(await As(owner).Events.ListAsync(catalog.Slug, new EventQuery { Limit = 2, Cursor = first.NextCursor }));

        first.Items.Should().HaveCount(2);
        first.NextCursor.Should().NotBeNullOrEmpty();
        second.Items.Should().ContainSingle();
        second.NextCursor.Should().BeNull();
        first.Items.Concat(second.Items).Select(item => item.Id).Should().BeEquivalentTo(events.Select(item => item.Id));
    }

    /// <summary>Another organization reads no event of a private catalog: 404, never 403.</summary>
    [Fact]
    public async Task GetAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var created = await CreateEventAsync(owner, catalog.Slug);
        var other = As(NewMember());

        Error(await other.Events.GetAsync(catalog.Slug, created.Id)).Type.Should().Be(AppErrorType.NotFound);
        Error(await other.Events.ListAsync(catalog.Slug, new EventQuery())).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>Another organization writes no event into a private catalog: 404.</summary>
    [Fact]
    public async Task CreateAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var catalog = await CreateCatalogAsync(NewMember());

        var created = await As(NewMember()).Events.CreateAsync(catalog.Slug, new EventSpec { Facts = Facts() });

        Error(created).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>A reader of a public catalog outside its owner sees published events only.</summary>
    [Fact]
    public async Task GetAsync_ShouldShowAReaderPublishedEventsOnly()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner, CatalogVisibility.Public);
        var published = await CreateEventAsync(owner, catalog.Slug);
        var candidate = await ProposeEventAsync(owner, catalog.Slug);
        var reader = As(NewMember());

        Value(await reader.Events.GetAsync(catalog.Slug, published.Id)).Id.Should().Be(published.Id);
        Error(await reader.Events.GetAsync(catalog.Slug, candidate.Id)).Type.Should().Be(AppErrorType.NotFound);
        Value(await reader.Events.ListAsync(catalog.Slug, new EventQuery { Review = EventReviewState.Candidate })).Items.Should().BeEmpty();
    }

    /// <summary>An update at the revision read replaces the facts and raises the revision.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldReplaceTheFacts_WhenTheRevisionMatches()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var created = await CreateEventAsync(owner, catalog.Slug);

        var updated = Value(await As(owner).Events.UpdateAsync(catalog.Slug, created.Id, new EventSpec { Facts = Facts("Renamed"), ExpectedRevision = created.Revision }));

        updated.Facts.Title[EventsConstants.UnknownLocale].Should().Be("Renamed");
        updated.Revision.Should().Be(created.Revision + 1);
    }

    /// <summary>An update at a stale revision conflicts.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldFailConflict_WhenTheRevisionIsStale()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var created = await CreateEventAsync(owner, catalog.Slug);
        Value(await As(owner).Events.UpdateAsync(catalog.Slug, created.Id, new EventSpec { Facts = Facts("First"), ExpectedRevision = created.Revision }));

        var stale = await As(owner).Events.UpdateAsync(catalog.Slug, created.Id, new EventSpec { Facts = Facts("Second"), ExpectedRevision = created.Revision });

        Error(stale).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>An archive at the revision read archives the event; a second one at the old revision conflicts.</summary>
    [Fact]
    public async Task ArchiveAsync_ShouldArchive_ThenFailConflict_AtTheOldRevision()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var created = await CreateEventAsync(owner, catalog.Slug);

        var archived = Value(await As(owner).Events.ArchiveAsync(catalog.Slug, created.Id, created.Revision));
        var again = await As(owner).Events.ArchiveAsync(catalog.Slug, created.Id, created.Revision);

        archived.Review.Should().Be(EventReviewState.Archived);
        archived.Revision.Should().Be(created.Revision + 1);
        Error(again).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>A key another event of the catalog holds conflicts.</summary>
    [Fact]
    public async Task CreateAsync_ShouldFailConflict_WhenTheKeyIsTaken()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        await CreateEventAsync(owner, catalog.Slug, Facts(key: "tashkent-fashion-week-2026"));

        var taken = await As(owner).Events.CreateAsync(catalog.Slug, new EventSpec { Facts = Facts(key: "tashkent-fashion-week-2026") });

        Error(taken).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>An image keeps its source URL and the Files id of SCFD's copy through a write and a read, in order; a
    /// <c>fileRef</c> that is no file id is invalid.</summary>
    [Fact]
    public async Task CreateAsync_ShouldKeepEachImagesSourceUrlAndFileRef_AndFailValidation_WhenAFileRefIsNoFileId()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var copy = Guid.NewGuid().ToString("D");
        var images = new[]
        {
            new EventImage { Url = "https://source.example/poster.jpg", FileRef = copy },
            new EventImage { Url = "https://source.example/second.jpg" },
        };

        var created = await CreateEventAsync(owner, catalog.Slug, Facts("Show with images") with { Images = images });
        var read = Value(await As(owner).Events.GetAsync(catalog.Slug, created.Id));
        var invalid = await As(owner).Events.CreateAsync(catalog.Slug,
            new EventSpec { Facts = Facts("Show with a bad ref") with { Images = [new EventImage { Url = "https://source.example/a.jpg", FileRef = "not-a-file-id" }] } });

        read.Facts.Images.Should().Equal(images);
        Error(invalid).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>Facts without a title are invalid.</summary>
    [Fact]
    public async Task CreateAsync_ShouldFailValidation_WhenTheTitleIsEmpty()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);

        var created = await As(owner).Events.CreateAsync(catalog.Slug, new EventSpec { Facts = Facts() with { Title = new Dictionary<string, string>() } });

        Error(created).Type.Should().Be(AppErrorType.Validation);
    }
}
