using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventDigestsBroker"/>: digests kept per organization and catalog, replaced at the
/// revision read, previewed with the texts a send now sends, and each send kept in the run history. Cases send through
/// the <see cref="DigestChannelConstants.Log"/> channel, which every implementation registers.</summary>
public abstract class EventDigestsBrokerConformance : EventsConformance
{
    /// <summary>A created digest reads back as written, with its next run.</summary>
    [Fact]
    public async Task CreateAsync_ShouldStoreTheDigest_WhenItIsValid()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);

        var created = Value(await As(owner).Digests.CreateAsync(catalog.Slug, Spec(owner.Subject)));

        created.Name.Should().Be("Weekly digest");
        created.Catalog.Should().Be(catalog.Slug);
        created.Schedule.Days.Should().Equal(DayOfWeek.Monday);
        created.Schedule.Times.Should().Equal("09:00");
        created.Schedule.TimeZone.Should().Be("Asia/Tashkent");
        created.Selection.NewSinceLastRun.Should().BeTrue();
        created.Recipients.Should().ContainSingle().Which.Subject.Should().Be(owner.Subject);
        created.NextRunAtUtc.Should().NotBeNull();
        created.Revision.Should().Be(1);
        Value(await As(owner).Digests.GetAsync(catalog.Slug, created.Id)).Should().BeEquivalentTo(created);
    }

    /// <summary>Another organization neither lists nor reads the digest.</summary>
    [Fact]
    public async Task Digests_ShouldBeKeptPerOrganization()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner, CatalogVisibility.Public);
        var created = Value(await As(owner).Digests.CreateAsync(catalog.Slug, Spec()));
        var other = NewMember();

        Value(await As(other).Digests.ListAsync(catalog.Slug)).Should().BeEmpty();
        Error(await As(other).Digests.GetAsync(catalog.Slug, created.Id)).Type.Should().Be(AppErrorType.NotFound);
        Value(await As(NewMember(owner.OrganizationId)).Digests.ListAsync(catalog.Slug)).Select(digest => digest.Id).Should().Equal(created.Id);
    }

    /// <summary>An update at the revision read replaces the digest; a stale one conflicts.</summary>
    [Fact]
    public async Task UpdateAsync_ShouldReplaceAtTheRevisionRead_AndConflictWhenStale()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var created = Value(await As(owner).Digests.CreateAsync(catalog.Slug, Spec()));

        var updated = Value(await As(owner).Digests.UpdateAsync(catalog.Slug, created.Id, Spec() with { Name = "Daily digest", ItemCap = 5, ExpectedRevision = created.Revision }));

        updated.Name.Should().Be("Daily digest");
        updated.ItemCap.Should().Be(5);
        updated.Revision.Should().BeGreaterThan(created.Revision);
        Error(await As(owner).Digests.UpdateAsync(catalog.Slug, created.Id, Spec() with { ExpectedRevision = created.Revision })).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>An unknown time zone, an unregistered channel and a recipient naming both a person and a role are invalid.</summary>
    [Fact]
    public async Task CreateAsync_ShouldFailValidation_WhenAFieldIsInvalid()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var broker = As(owner).Digests;

        Error(await broker.CreateAsync(catalog.Slug, Spec() with { Schedule = new DigestSchedule { Days = [DayOfWeek.Monday], Times = ["09:00"], TimeZone = "Mars/Olympus" } }))
            .Type.Should().Be(AppErrorType.Validation);
        Error(await broker.CreateAsync(catalog.Slug, Spec() with { Channel = "pigeon" })).Type.Should().Be(AppErrorType.Validation);
        Error(await broker.CreateAsync(catalog.Slug, Spec() with { Recipients = [new DigestRecipient { Subject = owner.Subject, Role = "member" }] }))
            .Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>A preview lists the new events in the texts a send now then sends, and writes no run.</summary>
    [Fact]
    public async Task PreviewAsync_ShouldRenderTheTextsASendNowSends_WithoutARun()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var digest = Value(await As(owner).Digests.CreateAsync(catalog.Slug, Spec()));
        var created = await CreateEventAsync(owner, catalog.Slug, Facts("Digest conformance fair", "2099-03-14"));

        var preview = Value(await As(owner).Digests.PreviewAsync(catalog.Slug, digest.Id));

        preview.Items.Select(item => item.EventId).Should().Equal(created.Id);
        preview.Items[0].IsNew.Should().BeTrue();
        preview.Texts.Should().ContainSingle().Which.Text.Should().Contain("Digest conformance fair");
        preview.Recipients.Should().ContainSingle().Which.Status.Should().Be(DigestRecipientStatusConstants.Pending);
        Value(await As(owner).Digests.ListRunsAsync(catalog.Slug, digest.Id, new DigestRunQuery())).Items.Should().BeEmpty();

        var run = Value(await As(owner).Digests.SendNowAsync(catalog.Slug, digest.Id));

        run.Texts.Select(text => text.Text).Should().Equal(preview.Texts.Select(text => text.Text));
    }

    /// <summary>A send now sends each recipient once, records the run, and a later run skips what it sent.</summary>
    [Fact]
    public async Task SendNowAsync_ShouldRecordTheRun_AndSkipItsEventsNextTime()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var digest = Value(await As(owner).Digests.CreateAsync(catalog.Slug, Spec()));
        var created = await CreateEventAsync(owner, catalog.Slug, Facts(starts: "2099-05-01"));

        var first = Value(await As(owner).Digests.SendNowAsync(catalog.Slug, digest.Id));
        var second = Value(await As(owner).Digests.SendNowAsync(catalog.Slug, digest.Id));

        first.Trigger.Should().Be(DigestRunTrigger.Manual);
        first.Status.Should().Be(DigestRunStatus.Completed);
        first.EventIds.Should().Equal(created.Id);
        first.Recipients.Should().ContainSingle().Which.Status.Should().Be(DigestRecipientStatusConstants.Sent);
        second.Status.Should().Be(DigestRunStatus.Empty);
        second.Selected.Should().Be(0);
        var runs = Value(await As(owner).Digests.ListRunsAsync(catalog.Slug, digest.Id, new DigestRunQuery()));
        runs.Items.Select(run => run.Id).Should().Equal(second.Id, first.Id);
        Value(await As(owner).Digests.GetAsync(catalog.Slug, digest.Id)).LastRunAtUtc.Should().Be(second.StartedAtUtc);
    }

    /// <summary>A delete at the revision read removes the digest.</summary>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheDigest()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);
        var digest = Value(await As(owner).Digests.CreateAsync(catalog.Slug, Spec()));

        Error(await As(owner).Digests.DeleteAsync(catalog.Slug, digest.Id, digest.Revision + 1)).Type.Should().Be(AppErrorType.Conflict);
        Value(await As(owner).Digests.DeleteAsync(catalog.Slug, digest.Id, digest.Revision)).Id.Should().Be(digest.Id);

        Error(await As(owner).Digests.GetAsync(catalog.Slug, digest.Id)).Type.Should().Be(AppErrorType.NotFound);
    }

    /// <summary>A caller without an organization has no digests.</summary>
    [Fact]
    public async Task ListAsync_ShouldFailForbidden_WithoutAnOrganization()
    {
        var owner = NewMember();
        var catalog = await CreateCatalogAsync(owner);

        Error(await As(EventsActor.Service()).Digests.ListAsync(catalog.Slug)).Type.Should().Be(AppErrorType.Forbidden);
    }

    private static DigestSpec Spec(string? recipient = null) => new()
    {
        Name = "Weekly digest",
        Channel = DigestChannelConstants.Log,
        Schedule = new DigestSchedule { Days = [DayOfWeek.Monday], Times = ["09:00"], TimeZone = "Asia/Tashkent" },
        Selection = new DigestSelection { NewSinceLastRun = true },
        Recipients = [new DigestRecipient { Subject = recipient ?? Guid.NewGuid().ToString("D") }],
        Language = DigestLanguageConstants.English,
    };
}
