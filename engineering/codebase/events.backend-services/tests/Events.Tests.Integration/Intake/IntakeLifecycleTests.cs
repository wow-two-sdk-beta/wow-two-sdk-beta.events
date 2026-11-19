using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Review;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using Events.Tests.Contract;
using Events.Tests.Integration.Catalogs;
using Events.Tests.Integration.Workflows;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;

namespace Events.Tests.Integration.Intake;

/// <summary>A post's lifecycle as SCFD reports it (the services sweep): a listing gone before its event holds an
/// automatic event for a person and releases it when the listing returns; so does a post merged into another, until it
/// is unmerged. A person's decision, a tracked event and a listing gone after its event change nothing, except that a
/// merged post's tracked event is held once its tracking moved to the survivor's event (E18).</summary>
public sealed class IntakeLifecycleTests(IntakeFixture fixture) : IClassFixture<IntakeFixture>
{
    [Fact]
    public async Task Intake_ShouldHoldAnAutoPublishedEvent_WhenItsListingIsGoneBeforeTheEvent_AndReleaseItWhenBack()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Show"));
        await fixture.PollAsync(source);
        var published = (await EventsAsync(slug, new EventQuery())).Single();

        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Show", type: "link.state", edit: data => data["state"] = "gone"));
        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty("a gone listing takes the event off the published feed");
        var held = (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single();
        held.Id.Should().Be(published.Id);
        held.ReviewedBy.Should().Be($"{IntakeProcessor.SourceActor}{source.Instance}");
        held.ReviewReason.Should().Be(IntakeProcessor.GoneReason);

        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Show", type: "link.state", edit: data => data["state"] = "live"));
        await fixture.PollAsync(source);

        var back = (await EventsAsync(slug, new EventQuery())).Single();
        back.Id.Should().Be(published.Id);
        back.ReviewedBy.Should().StartWith(IntakeVerdict.ClassifierActor, "the classifier reviews it again");
    }

    [Fact]
    public async Task Intake_ShouldKeepTheEvent_WhenItsListingIsGoneAfterTheEvent()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Past show", edit: Past));
        await fixture.PollAsync(source);

        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Past show", type: "post.changed", edit: data => { Past(data); data["state"] = "gone"; }));
        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery { From = new DateOnly(2026, 1, 1) })).Should().ContainSingle("listings end after their events");
    }

    [Fact]
    public async Task Intake_ShouldKeepAPersonsOrATrackedEvent_WhenItsListingIsGone()
    {
        var (slug, source) = await SourceAsync();
        var reviewed = Guid.NewGuid();
        var tracked = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(reviewed, 1, "Reviewed show"), ScfdRecords.Post(tracked, 1, "Tracked show"));
        await fixture.PollAsync(source);
        var events = await EventsAsync(slug, new EventQuery());
        var person = events.Single(item => item.Facts.Title.Values.Single() == "Reviewed show");
        (await Review(review => review.ReopenAsync(slug, person.Id, person.Revision))).ValueOrThrow();
        var reopened = (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single();
        (await Review(review => review.AcceptAsync(slug, reopened.Id, new ReviewDecision { ExpectedRevision = reopened.Revision }))).ValueOrThrow();
        await TrackAsync(events.Single(item => item.Facts.Title.Values.Single() == "Tracked show").Id);

        fixture.Feed.Append(source,
            ScfdRecords.Post(reviewed, 1, "Reviewed show", type: "link.state", edit: data => data["state"] = "gone"),
            ScfdRecords.Post(tracked, 1, "Tracked show", type: "link.state", edit: data => data["state"] = "gone"));
        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().HaveCount(2, "a person's decision and a tracked event stay published");
    }

    /// <summary>SCFD's merge is its own heuristic and can be undone (<c>post.unmerged</c>), so Events moves no fact: the
    /// merged post's event leaves the published feed as a duplicate held for a person, and returns whole on the unmerge.
    /// An untracked duplicate has no tracking to move (E18).</summary>
    [Fact]
    public async Task Intake_ShouldHoldTheDuplicate_WhenSCFDMergesItsPost_AndReleaseItWhenUnmerged()
    {
        var (slug, source) = await SourceAsync();
        var (loser, survivor) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(source, ScfdRecords.Post(survivor, 1, "Show, kept"), ScfdRecords.Post(loser, 1, "Show, duplicate"));
        await fixture.PollAsync(source);
        var duplicate = (await EventsAsync(slug, new EventQuery())).Single(item => item.Facts.Title.Values.Single() == "Show, duplicate");

        fixture.Feed.Append(source,
            ScfdRecords.Post(loser, 2, "Show, duplicate", type: "post.merged", edit: data => data["mergedInto"] = survivor),
            ScfdRecords.Post(survivor, 2, "Show, kept and merged", type: "post.changed"));
        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().ContainSingle().Which.Facts.Title.Values.Single().Should().Be("Show, kept and merged");
        var held = (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single();
        held.Id.Should().Be(duplicate.Id);
        held.ReviewedBy.Should().Be($"{IntakeProcessor.SourceActor}{source.Instance}");
        held.ReviewReason.Should().Be(IntakeProcessor.MergedReason);

        fixture.Feed.Append(source,
            ScfdRecords.Post(survivor, 3, "Show, kept", type: "post.unmerged"),
            ScfdRecords.Post(loser, 3, "Show, duplicate again", type: "post.unmerged"));
        await fixture.PollAsync(source);

        var after = await EventsAsync(slug, new EventQuery());
        after.Select(item => item.Facts.Title.Values.Single()).Should().BeEquivalentTo("Show, kept", "Show, duplicate again");
        after.Single(item => item.Id == duplicate.Id).ReviewedBy.Should().StartWith(IntakeVerdict.ClassifierActor);
    }

    /// <summary>E18: an organization's tracking of the merged post's event moves to the survivor's event, so the tracked
    /// duplicate stops listing twice: one listed event, in the stage the organization gave the duplicate. The duplicate keeps
    /// its own tracking and the carried one stays on the survivor when SCFD unmerges.</summary>
    [Fact]
    public async Task Intake_ShouldCarryTrackingToTheSurvivor_WhenSCFDMergesATrackedDuplicate_AndKeepItWhenUnmerged()
    {
        var (slug, source) = await TrackingSourceAsync();
        var (loser, survivor) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(source, ScfdRecords.Post(survivor, 1, "Show, kept"), ScfdRecords.Post(loser, 1, "Show, duplicate"));
        await fixture.PollAsync(source);
        var duplicate = (await EventsAsync(slug, new EventQuery())).Single(item => item.Facts.Title.Values.Single() == "Show, duplicate");
        var kept = (await EventsAsync(slug, new EventQuery())).Single(item => item.Facts.Title.Values.Single() == "Show, kept");
        (await TrackingAsync(tracking => tracking.PutAsync(slug, duplicate.Id, new TrackingSpec { Stage = "shortlisted", Priority = TrackingPriority.High, Notes = "call the organizer" }))).Stage.Should().Be("shortlisted");
        (await TrackingAsync(tracking => tracking.ListAsync(slug, new TrackingQuery()))).Items.Should().HaveCount(2, "the duplicate lists beside the survivor until SCFD merges them");

        fixture.Feed.Append(source,
            ScfdRecords.Post(loser, 2, "Show, duplicate", type: "post.merged", edit: data => data["mergedInto"] = survivor),
            ScfdRecords.Post(survivor, 2, "Show, kept and merged", type: "post.changed"));
        await fixture.PollAsync(source);

        var listed = (await TrackingAsync(tracking => tracking.ListAsync(slug, new TrackingQuery()))).Items.Should().ContainSingle("the duplicate is held and its tracking moved").Subject;
        listed.Event.Id.Should().Be(kept.Id);
        listed.Tracking.Should().NotBeNull().And.Match<TrackingModel>(item =>
            item.Stage == "shortlisted" && item.Priority == TrackingPriority.High && item.Notes == "call the organizer" && item.Revision == 1);
        var held = (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single();
        held.Id.Should().Be(duplicate.Id);
        held.ReviewReason.Should().Be(IntakeProcessor.MergedReason);
        var history = await TrackingAsync(tracking => tracking.GetHistoryAsync(slug, kept.Id));
        history.Should().ContainSingle().Which.Should().Match<TrackingHistoryEntryModel>(entry =>
            entry.FromStage == null && entry.ToStage == "shortlisted" && entry.ActorIssuer == null
            && entry.ActorSubject == $"{IntakeProcessor.SourceActor}{source.Instance} merged post {loser:D} into {survivor:D}");

        fixture.Feed.Append(source,
            ScfdRecords.Post(survivor, 3, "Show, kept", type: "post.unmerged"),
            ScfdRecords.Post(loser, 3, "Show, duplicate again", type: "post.unmerged"));
        await fixture.PollAsync(source);

        var again = (await TrackingAsync(tracking => tracking.ListAsync(slug, new TrackingQuery()))).Items;
        again.Should().HaveCount(2, "SCFD says they are two posts again");
        again.Select(item => item.Tracking!.Stage).Should().AllBe("shortlisted", "the carried tracking stays on the survivor, and the duplicate keeps its own");
    }

    /// <summary>E18: an organization tracking both events keeps the later stage by its own workflow's order and gets the
    /// duplicate's notes after its own; the survivor's tracking changes only when the merge adds something, and a history
    /// entry names the merge each time.</summary>
    [Theory]
    [InlineData("candidate", "applying", "applying")]
    [InlineData("confirmed", "shortlisted", "confirmed")]
    [InlineData("confirmed", "confirmed", "confirmed")]
    public async Task Intake_ShouldKeepTheLaterStageAndAppendNotes_WhenAnOrganizationTracksBothMergedEvents(string survivorStage, string duplicateStage, string expected)
    {
        var (slug, source) = await TrackingSourceAsync();
        var (loser, survivor) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(source, ScfdRecords.Post(survivor, 1, "Show, kept"), ScfdRecords.Post(loser, 1, "Show, duplicate"));
        await fixture.PollAsync(source);
        var events = await EventsAsync(slug, new EventQuery());
        var (duplicate, kept) = (events.Single(item => item.Facts.Title.Values.Single() == "Show, duplicate"), events.Single(item => item.Facts.Title.Values.Single() == "Show, kept"));
        await TrackingAsync(tracking => tracking.PutAsync(slug, kept.Id, new TrackingSpec { Stage = survivorStage, Assignee = "person-2", Notes = "survivor notes" }));
        await TrackingAsync(tracking => tracking.PutAsync(slug, duplicate.Id, new TrackingSpec { Stage = duplicateStage, Assignee = "person-3", Notes = "duplicate notes" }));
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        fixture.Feed.Append(source, ScfdRecords.Post(loser, 2, "Show, duplicate", type: "post.merged", edit: data => data["mergedInto"] = survivor));
        await fixture.PollAsync(source);

        var listed = (await TrackingAsync(tracking => tracking.ListAsync(slug, new TrackingQuery()))).Items.Should().ContainSingle().Subject;
        listed.Event.Id.Should().Be(kept.Id);
        listed.Tracking!.Stage.Should().Be(expected);
        listed.Tracking.Assignee.Should().Be("person-2", "the survivor's own tracking decides everything but the stage and the notes");
        listed.Tracking.Notes.Should().Be("survivor notes\n\nduplicate notes");
        listed.Tracking.Revision.Should().Be(2, "the merge changed the survivor's tracking once");
        var merge = (await TrackingAsync(tracking => tracking.GetHistoryAsync(slug, kept.Id))).Last();
        merge.FromStage.Should().Be(survivorStage);
        merge.ToStage.Should().Be(expected);
        merge.ActorSubject.Should().Contain($"merged post {loser:D} into {survivor:D}");
    }

    /// <summary>E18: each organization's own workflow orders its stages, and appended notes never pass the store's limit.</summary>
    [Fact]
    public async Task Intake_ShouldOrderStagesByEachOrganizationsWorkflow_AndKeepNotesWithinTheLimit()
    {
        var (slug, source) = await TrackingSourceAsync();
        var (loser, survivor) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(source, ScfdRecords.Post(survivor, 1, "Show, kept"), ScfdRecords.Post(loser, 1, "Show, duplicate"));
        await fixture.PollAsync(source);
        var events = await EventsAsync(slug, new EventQuery());
        var (duplicate, kept) = (events.Single(item => item.Facts.Title.Values.Single() == "Show, duplicate"), events.Single(item => item.Facts.Title.Values.Single() == "Show, kept"));

        // Another organization writes a workflow that ranks the stages the other way round; rows it holds on both events.
        var (issuer, other) = (fixture.Principal.Issuer!, Guid.NewGuid().ToString("N"));
        var owner = fixture.Principal.OrganizationId!;
        fixture.Principal.ActAsMember(other);
        await using (var scope = fixture.Scope())
        {
            var reversed = OcharoWorkflow.Spec() with { Stages = [.. OcharoWorkflow.Spec().Stages.Reverse()], DefaultStage = "rejected" };
            (await scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>().PutAsync(reversed)).ValueOrThrow();
        }

        fixture.Principal.ActAsMember(owner);
        await TrackRowAsync(kept.Id, issuer, other, "rejected", "other, survivor");
        await TrackRowAsync(duplicate.Id, issuer, other, "candidate", "other, duplicate");
        await TrackRowAsync(kept.Id, issuer, owner, "candidate", new string('a', EventsConstants.MaxNotesLength - 10));
        await TrackRowAsync(duplicate.Id, issuer, owner, "attended", new string('b', 100));

        fixture.Feed.Append(source, ScfdRecords.Post(loser, 2, "Show, duplicate", type: "post.merged", edit: data => data["mergedInto"] = survivor));
        await fixture.PollAsync(source);

        await using var check = fixture.Scope();
        var db = check.ServiceProvider.GetRequiredService<EventsDbContext>();
        var rows = await db.Tracking.AsNoTracking().Where(row => row.EventId == kept.Id).ToListAsync();
        var theirs = rows.Single(row => row.OrganizationId == other);
        theirs.Stage.Should().Be("candidate", "their workflow ranks candidate after rejected");
        theirs.Notes.Should().Be("other, survivor\n\nother, duplicate");
        var ours = rows.Single(row => row.OrganizationId == owner);
        ours.Stage.Should().Be("attended");
        ours.Notes.Should().HaveLength(EventsConstants.MaxNotesLength, "the appended notes are cut to the store's limit");
        (await db.Tracking.AsNoTracking().CountAsync(row => row.EventId == duplicate.Id)).Should().Be(2, "the duplicate keeps its own tracking");
    }

    /// <summary>E18: a tracked duplicate whose survivor SCFD never delivered has nowhere to move its tracking, so it stays
    /// published, as a tracked event always did.</summary>
    [Fact]
    public async Task Intake_ShouldKeepATrackedDuplicatePublished_WhenItsSurvivorPostHasNoEvent()
    {
        var (slug, source) = await TrackingSourceAsync();
        var loser = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(loser, 1, "Show, duplicate"));
        await fixture.PollAsync(source);
        var duplicate = (await EventsAsync(slug, new EventQuery())).Single();
        await TrackingAsync(tracking => tracking.PutAsync(slug, duplicate.Id, new TrackingSpec { Stage = "shortlisted" }));

        fixture.Feed.Append(source, ScfdRecords.Post(loser, 2, "Show, duplicate", type: "post.merged", edit: data => data["mergedInto"] = Guid.NewGuid()));
        await fixture.PollAsync(source);

        (await TrackingAsync(tracking => tracking.ListAsync(slug, new TrackingQuery()))).Items.Should().ContainSingle()
            .Which.Event.Id.Should().Be(duplicate.Id, "nothing moved, so the event stays");
    }

    /// <summary>E18: nothing to carry when no organization tracks the merged post's event: the survivor stays untracked.</summary>
    [Fact]
    public async Task Intake_ShouldCarryNothing_WhenNoOrganizationTracksTheMergedPostsEvent()
    {
        var (slug, source) = await TrackingSourceAsync();
        var (loser, survivor) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(source, ScfdRecords.Post(survivor, 1, "Show, kept"), ScfdRecords.Post(loser, 1, "Show, duplicate"));
        await fixture.PollAsync(source);

        fixture.Feed.Append(source, ScfdRecords.Post(loser, 2, "Show, duplicate", type: "post.merged", edit: data => data["mergedInto"] = survivor));
        await fixture.PollAsync(source);

        var listed = (await TrackingAsync(tracking => tracking.ListAsync(slug, new TrackingQuery()))).Items.Should().ContainSingle().Subject;
        listed.Event.Facts.Title.Values.Single().Should().Be("Show, kept");
        listed.Tracking.Should().BeNull();
    }

    /// <summary>SCFD sends <c>post.unmatched</c> once when a delivered post stops passing the subscription's filter
    /// (harvest E03): the off-topic event leaves the published feed, held by the source, and no listing gone or back
    /// releases it; only a revision that passes the filter again does.</summary>
    [Fact]
    public async Task Intake_ShouldHoldAnUntrackedEvent_WhenItsPostStopsMatching_AndKeepItHeldThroughItsListingsLifecycle()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Fashion week"));
        await fixture.PollAsync(source);
        var published = (await EventsAsync(slug, new EventQuery())).Single();

        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Concert", type: IntakeProcessor.UnmatchedType));
        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty("an unmatched post takes its event off the published feed");
        var held = (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single();
        held.Id.Should().Be(published.Id);
        held.Facts.Title.Values.Single().Should().Be("Fashion week", "the off-topic revision's content never applies");
        held.ReviewedBy.Should().Be($"{IntakeProcessor.SourceActor}{source.Instance}");
        held.ReviewReason.Should().Be(IntakeProcessor.UnmatchedReason);

        fixture.Feed.Append(source,
            ScfdRecords.Post(post, 2, "Concert", type: "link.state", edit: data => data["state"] = "gone"),
            ScfdRecords.Post(post, 2, "Concert", type: "link.state", edit: data => data["state"] = "live"));
        await fixture.PollAsync(source);

        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty("lifecycle records still arrive for an unmatched post and release nothing");
        (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single().ReviewReason.Should().Be(IntakeProcessor.UnmatchedReason);
    }

    [Fact]
    public async Task Intake_ShouldKeepATrackedEventPublished_WhenItsPostStopsMatching()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Tracked fashion week"));
        await fixture.PollAsync(source);
        var published = (await EventsAsync(slug, new EventQuery())).Single();
        await TrackAsync(published.Id);

        fixture.Feed.Append(source, ScfdRecords.Post(post, 2, "Concert", type: IntakeProcessor.UnmatchedType));
        await fixture.PollAsync(source);

        var kept = (await EventsAsync(slug, new EventQuery())).Should().ContainSingle("an organization tracks it").Subject;
        kept.Id.Should().Be(published.Id);
        kept.ReviewedBy.Should().StartWith(IntakeVerdict.ClassifierActor);
        kept.Facts.Title.Values.Single().Should().Be("Tracked fashion week");
    }

    [Fact]
    public async Task Intake_ShouldReleaseAnUnmatchedHold_WhenARevisionMatchesAgain_AndHoldItAsGone_WhenItsListingIsGone()
    {
        var (slug, source) = await SourceAsync();
        var (post, gone) = (Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Runway show"), ScfdRecords.Post(gone, 1, "Gone show"));
        await fixture.PollAsync(source);
        var published = await EventsAsync(slug, new EventQuery());

        fixture.Feed.Append(source,
            ScfdRecords.Post(post, 2, "Concert", type: IntakeProcessor.UnmatchedType),
            ScfdRecords.Post(gone, 2, "Concert", type: IntakeProcessor.UnmatchedType));
        await fixture.PollAsync(source);
        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty();

        fixture.Feed.Append(source,
            ScfdRecords.Post(post, 3, "Runway show, new date", type: "post.changed"),
            ScfdRecords.Post(gone, 3, "Gone show, matching", type: "post.changed", edit: data => data["state"] = "gone"));
        await fixture.PollAsync(source);

        var back = (await EventsAsync(slug, new EventQuery())).Should().ContainSingle().Subject;
        back.Id.Should().Be(published.Single(item => item.Facts.Title.Values.Single() == "Runway show").Id);
        back.Facts.Title.Values.Single().Should().Be("Runway show, new date", "the matching revision applies");
        back.ReviewedBy.Should().StartWith(IntakeVerdict.ClassifierActor, "the classifier reviews it again");
        var stillHeld = (await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate })).Single();
        stillHeld.Facts.Title.Values.Single().Should().Be("Gone show, matching");
        stillHeld.ReviewReason.Should().Be(IntakeProcessor.GoneReason, "it matches again, but its listing is gone before the event");
    }

    /// <summary>An SCFD selector change that admits a post again re-matches it at the revision Events already stores
    /// (<c>post.changed</c>, reason <c>resync</c>), never a newer one: the record is superseded as content, yet it is a revision
    /// that passes the selector, so it ends the unmatched hold and the source reads live again. A lifecycle record at that
    /// revision (<c>post.relisted</c>) still ends nothing.</summary>
    [Fact]
    public async Task Rematch_ShouldReleaseAnUnmatchedHold_AtTheSameRevision()
    {
        var (slug, source) = await SelectorSourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Beauty expo"));
        await fixture.PollAsync(source);
        var published = (await EventsAsync(slug, new EventQuery())).Single();

        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Beauty expo", type: IntakeProcessor.UnmatchedType) with { Reason = "resync" });
        await fixture.PollAsync(source);
        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty("the selector no longer holds the post");
        (await SourceOfAsync(slug, source, post)).State.Should().Be(EventSourceState.Unmatched);

        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Beauty expo", type: "post.relisted"));
        await fixture.PollAsync(source);
        (await EventsAsync(slug, new EventQuery())).Should().BeEmpty("a relist at the same revision is no match");
        (await SourceOfAsync(slug, source, post)).State.Should().Be(EventSourceState.Unmatched);

        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Beauty expo", type: "post.changed") with { Reason = "resync" });
        await fixture.PollAsync(source);

        var back = (await EventsAsync(slug, new EventQuery())).Should().ContainSingle("the selector admits the post again").Subject;
        back.Id.Should().Be(published.Id);
        back.ReviewedBy.Should().StartWith(IntakeVerdict.ClassifierActor, "the classifier reviews it again");
        (await SourceOfAsync(slug, source, post)).State.Should().Be(EventSourceState.Live);
    }

    /// <summary>A selector subscription's source ref names its source in the record's payload <c>context</c> (<c>sourceName</c>,
    /// else <c>sourceGroup</c>), ahead of the delivery's own context and the intake source's name: one intake source reads
    /// "via CAEx" and "via Uzexpocentre" from the records it pulls.</summary>
    [Fact]
    public async Task SelectorSource_ShouldNameTheSourceFromThePayloadContext()
    {
        var (slug, source) = await SelectorSourceAsync();
        var (named, grouped, enveloped, bare) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        fixture.Feed.Append(source,
            ScfdRecords.Post(named, 1, "Named", edit: data => data["context"] = new { catalog = "ocharo-fashion", sourceGroup = "caex", sourceName = "CAEx" })
                with { Context = Context(("sourceName", "Envelope name")) },
            ScfdRecords.Post(grouped, 1, "Grouped", edit: data => data["context"] = new { catalog = "ocharo-fashion", sourceGroup = "Uzexpocentre" })
                with { Context = Context(("sourceName", "Envelope name")) },
            ScfdRecords.Post(enveloped, 1, "Enveloped") with { Context = Context(("sourceGroup", "Afisha Uzbekistan")) },
            ScfdRecords.Post(bare, 1, "Bare"));

        await fixture.PollAsync(source);

        (await SourceOfAsync(slug, source, named)).Name.Should().Be("CAEx", "the payload's sourceName comes first");
        (await SourceOfAsync(slug, source, grouped)).Name.Should().Be("Uzexpocentre", "the payload's sourceGroup beats the envelope's sourceName");
        (await SourceOfAsync(slug, source, enveloped)).Name.Should().Be("Afisha Uzbekistan", "no payload context: the delivery's context names it as before");
        (await SourceOfAsync(slug, source, bare)).Name.Should().Be(source.Name, "nothing names it: the intake source's name stands");
    }

    /// <summary>SCFD's golden pages through the real intake (E15's check): the post's event source takes the intake source's
    /// name (the pages' context names no source), the posted time and each state the lifecycle page reports, gone and live
    /// again.</summary>
    [Fact]
    public async Task Intake_ShouldKeepTheEventSourcesNameStateAndPostedTime_ThroughTheLifecyclePage()
    {
        var (slug, source) = await SourceAsync();
        var created = (await PageAsync("01-new.json")).Single();
        var lifecycle = await PageAsync("05-lifecycle.json");
        fixture.Feed.Append(source, created);
        await fixture.PollAsync(source);

        var first = await SourceOfAsync(slug, source, created.PostId!.Value);
        first.Name.Should().Be(source.Name, "the record's context names no source, so the intake source's name stands");
        first.State.Should().Be(EventSourceState.Live);
        first.PostedAtUtc.Should().Be(new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero));

        fixture.Feed.Append(source, lifecycle[0], lifecycle[1]);
        await fixture.PollAsync(source);
        lifecycle[1].Type.Should().Be("link.state");
        (await SourceOfAsync(slug, source, created.PostId.Value)).State.Should().Be(EventSourceState.Gone);

        fixture.Feed.Append(source, [.. lifecycle.Skip(2)]);
        await fixture.PollAsync(source);
        (await SourceOfAsync(slug, source, created.PostId.Value)).Should().Match<EventSourceModel>(item =>
            item.State == EventSourceState.Live && item.PostedAtUtc == first.PostedAtUtc && item.Name == source.Name);
        (await SourceOfAsync(slug, source, lifecycle[0].PostId!.Value)).State.Should().Be(EventSourceState.Live);
    }

    /// <summary>The tracking read carries each source's name, state and posted time (E15): the subscription's source name
    /// over its group; <c>unmatched</c> stays through the listing's own records until a revision passes the filter again,
    /// while the tracked event stays published (E04).</summary>
    [Fact]
    public async Task TrackingRead_ShouldCarryTheSourcesNameStateAndPostedTime_AndKeepUnmatchedUntilARevisionMatches()
    {
        var (slug, source) = await SourceAsync();
        var post = Guid.NewGuid();
        fixture.Feed.Append(source, ScfdRecords.Post(post, 1, "Show", edit: Posted) with { Context = Context(("sourceGroup", "Afisha Uzbekistan")) });
        await fixture.PollAsync(source);
        var tracked = await TrackedAsync(slug);
        tracked.Event.Sources.Single().Should().Match<EventSourceModel>(item =>
            item.Name == "Afisha Uzbekistan" && item.State == EventSourceState.Live && item.PostedAtUtc == new DateTimeOffset(2026, 11, 8, 4, 0, 0, TimeSpan.Zero));
        await TrackAsync(tracked.Event.Id);

        fixture.Feed.Append(source,
            ScfdRecords.Post(post, 2, "Show", type: "post.changed", edit: Posted) with { Context = Context(("sourceGroup", "Afisha Uzbekistan"), ("sourceName", "Afisha")) },
            ScfdRecords.Post(post, 3, "Concert", type: IntakeProcessor.UnmatchedType) with { Context = Context(("sourceName", "Afisha")) },
            ScfdRecords.Post(post, 3, "Concert", type: "link.state") with { Context = Context(("sourceName", "Afisha")) });
        await fixture.PollAsync(source);

        var unmatched = (await TrackingAsync(tracking => tracking.ListAsync(slug, new TrackingQuery { TrackedOnly = true }))).Items.Single();
        unmatched.Event.Review.Should().Be(EventReviewState.Published, "an organization tracks it");
        unmatched.Event.Sources.Single().Should().Match<EventSourceModel>(item =>
            item.Name == "Afisha" && item.State == EventSourceState.Unmatched && item.PostedAtUtc == new DateTimeOffset(2026, 11, 8, 4, 0, 0, TimeSpan.Zero));

        fixture.Feed.Append(source, ScfdRecords.Post(post, 4, "Show again", type: "post.changed") with { Context = Context(("sourceName", "Afisha")) });
        await fixture.PollAsync(source);

        (await TrackedAsync(slug)).Event.Sources.Single().State.Should().Be(EventSourceState.Live, "a revision passed the filter again");
    }

    private static void Posted(Dictionary<string, object?> data) =>
        data["postedAt"] = new { value = "2026-11-08T09:00:00+05:00", precision = "second", timeZone = "Asia/Tashkent" };

    private static Dictionary<string, string> Context(params (string Key, string Value)[] pairs) =>
        pairs.Append(("catalog", "ocharo-fashion")).ToDictionary(pair => pair.Item1, pair => pair.Item2);

    private static async Task<IReadOnlyList<ScfdDeliveryRecord>> PageAsync(string name)
    {
        var (feed, _) = ContractPages.Feed(name);
        return (await feed.ReadAsync(ContractPages.Source(), cursor: null, limit: 50, CancellationToken.None)).Records;
    }

    private async Task<EventSourceModel> SourceOfAsync(string slug, EventsIntakeSourceOptions source, Guid post)
    {
        var id = $"{source.Instance}/{post:D}";
        var events = (await EventsAsync(slug, new EventQuery())).Concat(await EventsAsync(slug, new EventQuery { Review = EventReviewState.Candidate }));
        return events.SelectMany(item => item.Sources).Single(item => item.Id == id);
    }

    private async Task<TrackedEventModel> TrackedAsync(string slug) =>
        (await TrackingAsync(tracking => tracking.ListAsync(slug, new TrackingQuery()))).Items.Single();

    private async Task<T> TrackingAsync<T>(Func<IEventTrackingBroker, Task<Result<T>>> call) where T : notnull
    {
        await using var scope = fixture.Scope();
        return (await call(scope.ServiceProvider.GetRequiredService<IEventTrackingBroker>())).ValueOrThrow();
    }

    private static void Past(Dictionary<string, object?> data) => data["starts"] = new { value = "2026-10-01", precision = "day" };

    private async Task<(string Slug, EventsIntakeSourceOptions Source)> SourceAsync(Action<EventsIntakeSourceOptions>? configure = null)
    {
        fixture.Principal.ActAsMember(Guid.NewGuid().ToString("N"));
        var slug = $"lifecycle-{Guid.NewGuid():N}";
        await TestCatalogs.CreateAsync(fixture.Services, slug, CatalogVisibility.Private, "runway");
        return (slug, fixture.AddSource(slug, auto =>
        {
            auto.Review = EventsIntakeSourceOptions.AutoReview;
            configure?.Invoke(auto);
        }));
    }

    /// <summary>A source bound to an SCFD selector subscription: its filter names the selector, and the subscription answers
    /// as that selector does (no <c>filterKey</c>, kind and name instead).</summary>
    private async Task<(string Slug, EventsIntakeSourceOptions Source)> SelectorSourceAsync()
    {
        const string name = "ocharo-events";
        var (slug, source) = await SourceAsync(auto => auto.Filter = $"selector:{name}");
        fixture.Feed.Subscription(source, new ScfdSubscription(null, EventsConstants.EventSchema, ScfdSubscription.ActiveState, "selector", name));
        return (slug, source);
    }

    /// <summary>An auto-reviewed source in a catalog the acting organization owns, with Ocharo's workflow written for it
    /// (its stages include <c>shortlisted</c>), so tests track through the broker.</summary>
    private async Task<(string Slug, EventsIntakeSourceOptions Source)> TrackingSourceAsync()
    {
        var result = await SourceAsync();
        await using var scope = fixture.Scope();
        (await scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>().PutAsync(OcharoWorkflow.Spec())).ValueOrThrow();
        return result;
    }

    private async Task TrackRowAsync(Guid eventId, string issuer, string organization, string stage, string notes)
    {
        await using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.Tracking.Add(new TrackingEntity
        {
            Id = Guid.NewGuid(), Issuer = issuer, OrganizationId = organization, EventId = eventId, Stage = stage, Notes = notes,
            Revision = 1, CreatedAt = fixture.Clock.GetUtcNow(), UpdatedAt = fixture.Clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
    }

    private async Task TrackAsync(Guid eventId)
    {
        await using var scope = fixture.Scope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.Tracking.Add(new TrackingEntity
        {
            Id = Guid.NewGuid(), Issuer = fixture.Principal.Issuer!, OrganizationId = fixture.Principal.OrganizationId!, EventId = eventId,
            Stage = "candidate", Revision = 1, CreatedAt = fixture.Clock.GetUtcNow(), UpdatedAt = fixture.Clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<EventModel>> EventsAsync(string slug, EventQuery query)
    {
        await using var scope = fixture.Scope();
        return (await scope.ServiceProvider.GetRequiredService<ICatalogEventsBroker>().ListAsync(slug, query)).ValueOrThrow().Items;
    }

    private async Task<T> Review<T>(Func<IEventReviewBroker, Task<T>> call)
    {
        await using var scope = fixture.Scope();
        return await call(scope.ServiceProvider.GetRequiredService<IEventReviewBroker>());
    }
}
