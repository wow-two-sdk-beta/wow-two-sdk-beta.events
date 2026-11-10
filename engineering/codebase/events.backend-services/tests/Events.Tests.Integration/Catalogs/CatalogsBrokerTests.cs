using AwesomeAssertions;
using Events.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace Events.Tests.Integration.Catalogs;

/// <summary>The module's catalogs: who creates, replaces and reads them.</summary>
public sealed class CatalogsBrokerTests(ModuleFixture fixture) : IClassFixture<ModuleFixture>
{
    [Fact]
    public async Task PutAsync_ShouldCreateTheCatalog_ForTheCallersOrganization()
    {
        var organization = ModuleFixture.NewOrganization();
        var slug = ModuleFixture.NewSlug();
        fixture.Principal.ActAsMember(organization);

        var created = (await Catalogs(broker => broker.PutAsync(slug, new CatalogSpec
        {
            Name = "  Ocharo fashion ",
            Kinds = [new CatalogKindModel { Key = "runway", Label = new Dictionary<string, string> { ["en"] = "Runway" } }],
        }))).ValueOrThrow();

        created.Name.Should().Be("Ocharo fashion");
        created.OwnerOrganizationId.Should().Be(organization);
        created.Visibility.Should().Be(CatalogVisibility.Private);
        created.Revision.Should().Be(1);
        created.Kinds.Should().ContainSingle().Which.Label["en"].Should().Be("Runway");
        (await Catalogs(broker => broker.GetAsync(slug))).ValueOrThrow().Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task PutAsync_ShouldRequireAnOrganization_WhenTheCallerHasNone()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        fixture.Principal.OrganizationId = null;

        var result = await Catalogs(broker => broker.PutAsync(ModuleFixture.NewSlug(), new CatalogSpec { Name = "No organization" }));

        TestResults.ErrorOf(result).Type.Should().Be(AppErrorType.Forbidden);
    }

    [Fact]
    public async Task PutAsync_ShouldReplace_OnlyWithTheRevisionRead()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var slug = ModuleFixture.NewSlug();
        await TestCatalogs.CreateAsync(fixture.Services, slug);

        var missing = await Catalogs(broker => broker.PutAsync(slug, new CatalogSpec { Name = "Renamed" }));
        var replaced = (await Catalogs(broker => broker.PutAsync(slug, new CatalogSpec { Name = "Renamed", ExpectedRevision = 1 }))).ValueOrThrow();
        var stale = await Catalogs(broker => broker.PutAsync(slug, new CatalogSpec { Name = "Again", ExpectedRevision = 1 }));

        TestResults.ErrorOf(missing).Type.Should().Be(AppErrorType.Validation);
        replaced.Name.Should().Be("Renamed");
        replaced.Revision.Should().Be(2);
        TestResults.ErrorOf(stale).Type.Should().Be(AppErrorType.Conflict);
    }

    [Fact]
    public async Task PutAsync_ShouldRefuseAnotherOrganization()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var open = ModuleFixture.NewSlug();
        var closed = ModuleFixture.NewSlug();
        await TestCatalogs.CreateAsync(fixture.Services, open, CatalogVisibility.Public);
        await TestCatalogs.CreateAsync(fixture.Services, closed);

        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var onPublic = await Catalogs(broker => broker.PutAsync(open, new CatalogSpec { Name = "Taken", ExpectedRevision = 1 }));
        var onPrivate = await Catalogs(broker => broker.PutAsync(closed, new CatalogSpec { Name = "Taken", ExpectedRevision = 1 }));

        TestResults.ErrorOf(onPublic).Message.Should().Be(EventsErrors.NotCatalogOwner().Message);
        TestResults.ErrorOf(onPrivate).Message.Should().Be(EventsErrors.CatalogNotFound().Message);
    }

    [Fact]
    public async Task GetAsync_ShouldNotFindTheCatalog_WhenAnotherOrganizationReadsAPrivateCatalog()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var slug = ModuleFixture.NewSlug();
        await TestCatalogs.CreateAsync(fixture.Services, slug);

        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var result = await Catalogs(broker => broker.GetAsync(slug));

        TestResults.ErrorOf(result).Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task ListAsync_ShouldListOwnedAndPublicCatalogs()
    {
        var owner = ModuleFixture.NewOrganization();
        fixture.Principal.ActAsMember(owner);
        var mine = ModuleFixture.NewSlug();
        await TestCatalogs.CreateAsync(fixture.Services, mine);
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var theirsPublic = ModuleFixture.NewSlug();
        var theirsPrivate = ModuleFixture.NewSlug();
        await TestCatalogs.CreateAsync(fixture.Services, theirsPublic, CatalogVisibility.Public);
        await TestCatalogs.CreateAsync(fixture.Services, theirsPrivate);

        fixture.Principal.ActAsMember(owner);
        var listed = (await Catalogs(broker => broker.ListAsync())).ValueOrThrow().Select(catalog => catalog.Slug).ToList();
        fixture.Principal.ActAsAnonymous();
        var anonymous = (await Catalogs(broker => broker.ListAsync())).ValueOrThrow();

        listed.Should().Contain([mine, theirsPublic]).And.NotContain(theirsPrivate);
        anonymous.Should().BeEmpty();
    }

    [Fact]
    public async Task PutAsync_ShouldRejectAnInvalidSlugOrKinds()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());

        var slug = await Catalogs(broker => broker.PutAsync("Not A Slug", new CatalogSpec { Name = "Bad" }));
        var kinds = await Catalogs(broker => broker.PutAsync(ModuleFixture.NewSlug(), new CatalogSpec
        {
            Name = "Twice",
            Kinds = [new CatalogKindModel { Key = "runway" }, new CatalogKindModel { Key = "runway" }],
        }));

        TestResults.ErrorOf(slug).Type.Should().Be(AppErrorType.Validation);
        TestResults.ErrorOf(kinds).Message.Should().Contain("twice");
    }

    [Fact]
    public async Task PutAsync_ShouldKeepTheCatalogPrivate_WhenItHoldsSourcedRecords()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        var slug = ModuleFixture.NewSlug();
        var catalog = await TestCatalogs.CreateAsync(fixture.Services, slug);
        await SeedSourcedEventAsync(slug);

        var opened = await Catalogs(broker => broker.PutAsync(slug, new CatalogSpec
        {
            Name = catalog.Name, Visibility = CatalogVisibility.Public, ExpectedRevision = catalog.Revision,
        }));
        var renamed = await Catalogs(broker => broker.PutAsync(slug, new CatalogSpec { Name = "Still private", ExpectedRevision = catalog.Revision }));

        TestResults.ErrorOf(opened).Type.Should().Be(AppErrorType.Validation);
        TestResults.ErrorOf(opened).Message.Should().StartWith("visibility");
        renamed.ValueOrThrow().Visibility.Should().Be(CatalogVisibility.Private);
    }

    private async Task SeedSourcedEventAsync(string slug)
    {
        var now = fixture.Clock.GetUtcNow();
        await using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var catalog = db.Catalogs.Single(row => row.Slug == slug);
        var id = Guid.CreateVersion7(now);
        db.Events.Add(new CatalogEventEntity
        {
            Id = id, CatalogId = catalog.Id, Review = "candidate", Facts = """{"title":{"und":"Sourced"}}""", TitleSort = "sourced",
            Search = "sourced", Revision = 1, CreatedAt = now, UpdatedAt = now,
        });
        db.Sources.Add(new EventSourceEntity
        {
            CatalogId = catalog.Id, Kind = "scfd", SourceId = $"scfd/{Guid.NewGuid():D}", EventId = id, FirstSeenAt = now, LastSeenAt = now,
        });
        await db.SaveChangesAsync();
    }

    private async Task<T> Catalogs<T>(Func<IEventCatalogsBroker, Task<T>> call)
    {
        await using var scope = fixture.Scope();
        return await call(scope.ServiceProvider.GetRequiredService<IEventCatalogsBroker>());
    }
}
