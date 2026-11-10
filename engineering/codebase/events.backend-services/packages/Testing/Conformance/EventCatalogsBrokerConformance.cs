using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventCatalogsBroker"/>: a put creates for the caller's organization or replaces a
/// catalog it owns at the revision it read; a private catalog reads for its owner only, and answers 404 to anyone else.</summary>
public abstract class EventCatalogsBrokerConformance : EventsConformance
{
    /// <summary>A put of a new slug creates the catalog, owned by the caller's organization.</summary>
    [Fact]
    public async Task PutAsync_ShouldCreateTheCatalog_ForTheCallersOrganization()
    {
        var owner = NewMember();
        var slug = NewSlug();

        var catalog = Value(await As(owner).Catalogs.PutAsync(slug, new CatalogSpec { Name = "Fashion weeks", Locales = ["uz", "en"] }));

        catalog.Slug.Should().Be(slug);
        catalog.Name.Should().Be("Fashion weeks");
        catalog.OwnerOrganizationId.Should().Be(owner.OrganizationId);
        catalog.OwnerIssuer.Should().NotBeNullOrWhiteSpace();
        catalog.Visibility.Should().Be(CatalogVisibility.Private);
        catalog.Locales.Should().Equal("uz", "en");
        catalog.Revision.Should().Be(1);
    }

    /// <summary>The owner reads the catalog back, and finds it in its list.</summary>
    [Fact]
    public async Task GetAsync_ShouldReturnTheCatalog_ToItsOwner()
    {
        var owner = NewMember();
        var created = await CreateCatalogAsync(owner);

        var read = Value(await As(owner).Catalogs.GetAsync(created.Slug));
        var listed = Value(await As(owner).Catalogs.ListAsync());

        read.Should().BeEquivalentTo(created);
        listed.Select(catalog => catalog.Slug).Should().Contain(created.Slug);
    }

    /// <summary>Another member of the owner organization reads it too.</summary>
    [Fact]
    public async Task GetAsync_ShouldReturnTheCatalog_ToAnotherMemberOfTheOwner()
    {
        var owner = NewMember();
        var created = await CreateCatalogAsync(owner);

        var read = Value(await As(NewMember(owner.OrganizationId)).Catalogs.GetAsync(created.Slug));

        read.Slug.Should().Be(created.Slug);
    }

    /// <summary>Another organization neither reads nor lists a private catalog: 404, never 403.</summary>
    [Fact]
    public async Task GetAsync_ShouldFailNotFound_ForAnotherOrganization()
    {
        var created = await CreateCatalogAsync(NewMember());
        var other = As(NewMember());

        Error(await other.Catalogs.GetAsync(created.Slug)).Type.Should().Be(AppErrorType.NotFound);
        Value(await other.Catalogs.ListAsync()).Select(catalog => catalog.Slug).Should().NotContain(created.Slug);
    }

    /// <summary>An unknown slug is not found.</summary>
    [Fact]
    public async Task GetAsync_ShouldFailNotFound_WhenTheSlugIsUnknown()
    {
        Error(await As(NewMember()).Catalogs.GetAsync(NewSlug())).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>A put at the revision read replaces the catalog and raises its revision.</summary>
    [Fact]
    public async Task PutAsync_ShouldReplaceTheCatalog_WhenTheRevisionMatches()
    {
        var owner = NewMember();
        var created = await CreateCatalogAsync(owner);

        var replaced = Value(await As(owner).Catalogs.PutAsync(created.Slug, new CatalogSpec { Name = "Renamed", ExpectedRevision = created.Revision }));

        replaced.Name.Should().Be("Renamed");
        replaced.Revision.Should().Be(created.Revision + 1);
    }

    /// <summary>A put at a stale revision conflicts and changes nothing.</summary>
    [Fact]
    public async Task PutAsync_ShouldFailConflict_WhenTheRevisionIsStale()
    {
        var owner = NewMember();
        var created = await CreateCatalogAsync(owner);
        Value(await As(owner).Catalogs.PutAsync(created.Slug, new CatalogSpec { Name = "First", ExpectedRevision = created.Revision }));

        var stale = await As(owner).Catalogs.PutAsync(created.Slug, new CatalogSpec { Name = "Second", ExpectedRevision = created.Revision });

        Error(stale).Type.Should().Be(AppErrorType.Conflict);
        Value(await As(owner).Catalogs.GetAsync(created.Slug)).Name.Should().Be("First");
    }

    /// <summary>Another organization's put of a taken slug fails and leaves the owner's catalog as it was.</summary>
    [Fact]
    public async Task PutAsync_ShouldLeaveTheCatalog_WhenAnotherOrganizationPutsItsSlug()
    {
        var owner = NewMember();
        var created = await CreateCatalogAsync(owner);

        var taken = await As(NewMember()).Catalogs.PutAsync(created.Slug, new CatalogSpec { Name = "Hijacked", ExpectedRevision = created.Revision });

        Error(taken).Type.Should().Be(AppErrorType.NotFound);
        Value(await As(owner).Catalogs.GetAsync(created.Slug)).Should().BeEquivalentTo(created);
    }

    /// <summary>A slug outside the key pattern is invalid.</summary>
    [Fact]
    public async Task PutAsync_ShouldFailValidation_WhenTheSlugIsMalformed()
    {
        Error(await As(NewMember()).Catalogs.PutAsync("Not A Slug", new CatalogSpec { Name = "Bad" })).Type.Should().Be(AppErrorType.Validation);
    }
}
