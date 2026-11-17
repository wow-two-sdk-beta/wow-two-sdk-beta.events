using AwesomeAssertions;
using Events.Tests.Integration.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Identity.Notices;

namespace Events.Tests.Integration.Digests;

/// <summary>Covers the digests' scheduler on a fake clock: an occurrence runs once at its UTC instant, a restarted host or
/// a second host never runs it again, a host that died mid-run is resumed under the same idempotency keys, passing
/// failures are retried, and an occurrence older than the catch-up window is recorded missed.</summary>
/// <param name="fixture">The module's database.</param>
public sealed class DigestSchedulerTests(DigestFixture fixture) : IClassFixture<DigestFixture>
{
    // Sunday 2026-11-22 12:00 UTC; the next Monday 09:00 in Tashkent (UTC+5) is 2026-11-23 04:00 UTC.
    private static readonly DateTimeOffset Sunday = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset MondayAt0400Utc = new(2026, 11, 23, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Scheduler_ShouldRunAMondayTashkentDigestOnceAt0400Utc_AndNotAgainAfterARestart()
    {
        var notices = new FakeAppNoticeBroker();
        var person = Linked(notices);
        var organization = Guid.NewGuid().ToString("D");
        Guid digestId;
        await using (var host = fixture.StartHost(new FakeTimeProvider(Sunday), notices))
        {
            await host.DisableOtherDigestsAsync();
            var catalog = await host.CatalogAsync(organization);
            await host.EventAsync(catalog, Facts("Tashkent Fashion Week", "2026-12-01"));
            host.Principal.ActAsMember(organization);
            var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Weekly(person)))).Value();
            digestId = digest.Id;
            digest.NextRunAtUtc.Should().Be(MondayAt0400Utc);

            host.Clock.SetUtcNow(MondayAt0400Utc.AddSeconds(-1));
            (await host.TurnAsync()).Should().Be(0, "the occurrence is not due a second early");
            (await host.RunsAsync(digestId)).Should().BeEmpty();

            host.Clock.SetUtcNow(MondayAt0400Utc);
            (await host.TurnAsync()).Should().Be(1);
            (await host.TurnAsync()).Should().Be(0, "a second turn finds the occurrence run");
        }

        // A restarted host, and a second host that read the digest as due before the first claimed it.
        await using (var restarted = fixture.StartHost(new FakeTimeProvider(MondayAt0400Utc.AddSeconds(30)), notices))
        {
            (await restarted.TurnAsync()).Should().Be(0);
            await restarted.StoreAsync(db => db.Digests.Where(row => row.Id == digestId).ExecuteUpdateAsync(set => set.SetProperty(row => row.NextRunAt, MondayAt0400Utc)));
            (await restarted.TurnAsync()).Should().Be(0, "the run row is unique on digest and occurrence");

            var runs = await restarted.RunsAsync(digestId);
            runs.Should().ContainSingle();
            runs[0].OccurrenceAt.Should().Be(MondayAt0400Utc);
            runs[0].Trigger.Should().Be("schedule");
            runs[0].Status.Should().Be("completed");
            notices.Sent.Where(notice => notice.Subject == person).Should().ContainSingle()
                .Which.Text.Should().Contain("Tashkent Fashion Week");
            (await restarted.StoreAsync(db => db.Digests.SingleAsync(row => row.Id == digestId))).NextRunAt
                .Should().Be(MondayAt0400Utc.AddDays(7), "the claim moved the next run to the following Monday");
        }
    }

    [Fact]
    public async Task Scheduler_ShouldResumeARunWhoseHostDied_WithTheSameIdempotencyKeys()
    {
        var notices = new FakeAppNoticeBroker();
        var first = Linked(notices);
        var second = Linked(notices);
        var organization = Guid.NewGuid().ToString("D");
        await using var host = fixture.StartHost(new FakeTimeProvider(Sunday), notices);
        await host.DisableOtherDigestsAsync();
        var catalog = await host.CatalogAsync(organization);
        await host.EventAsync(catalog, Facts("Craft fair", "2026-12-05"));
        host.Principal.ActAsMember(organization);
        var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Weekly(first, second)))).Value();

        // The dead host claimed the occurrence and reached Identity for the first person, but died before recording it.
        host.Clock.SetUtcNow(MondayAt0400Utc);
        Guid? runId;
        await using (var scope = host.Services.CreateAsyncScope())
            runId = await scope.ServiceProvider.GetRequiredService<DigestRunner>().StartDueAsync(digest.Id, CancellationToken.None);
        runId.Should().NotBeNull();
        var text = (await host.RunsAsync(digest.Id)).Single().Texts;
        var sentText = EventsJson.Read<List<DigestTextModel>>(text).Single().Text;
        (await notices.SendNoticeAsync(new AppNotice { Subject = first, Text = sentText, IdempotencyKey = DigestRunner.IdempotencyKey(runId!.Value, first), Culture = "uz-Latn" })).Value();
        await host.StoreAsync(db => db.DigestRuns.Where(run => run.Id == runId).ExecuteUpdateAsync(set => set.SetProperty(run => run.LeaseUntil, MondayAt0400Utc.AddMinutes(5))));

        host.Clock.SetUtcNow(MondayAt0400Utc.AddMinutes(1));
        await host.TurnAsync();
        (await host.StoreAsync(db => db.DigestDeliveries.CountAsync(row => row.RunId == runId && row.Status == "pending"))).Should().Be(2, "the dead host's lease holds");

        host.Clock.SetUtcNow(MondayAt0400Utc.AddMinutes(6));
        await host.TurnAsync();

        var page = (await host.DigestsAsync(broker => broker.ListRunsAsync(catalog.Slug, digest.Id, new DigestRunQuery()))).Value();
        page.Items.Should().ContainSingle().Which.Status.Should().Be(DigestRunStatus.Completed);
        page.Items[0].Recipients.Select(recipient => (recipient.Subject, recipient.Status)).Should().Equal(
            (first, DigestRecipientStatusConstants.Sent), (second, DigestRecipientStatusConstants.Sent));
        notices.Sent.Count(notice => notice.Subject == first).Should().Be(1, "the resent key answers its first outcome and sends nothing more");
        notices.Sent.Count(notice => notice.Subject == second).Should().Be(1);
    }

    [Fact]
    public async Task SendNow_ShouldRetryARateLimitedRecipientAfterItsRetryAfter()
    {
        var notices = new FakeAppNoticeBroker();
        var person = Linked(notices);
        var organization = Guid.NewGuid().ToString("D");
        await using var host = fixture.StartHost(new FakeTimeProvider(Sunday), notices);
        await host.DisableOtherDigestsAsync();
        var catalog = await host.CatalogAsync(organization);
        await host.EventAsync(catalog, Facts("Textile expo", "2026-12-10"));
        host.Principal.ActAsMember(organization);
        var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Weekly(person)))).Value();
        notices.FailNext(AppError.Of(AppErrorType.TooManyRequests, "Slow down.", new Dictionary<string, object?>
        {
            [AppNoticeConstants.CodeMetadataKey] = AppNoticeErrorCodeConstants.RateLimited,
            [AppNoticeConstants.RetryAfterMetadataKey] = 60,
        }));

        var run = (await host.DigestsAsync(broker => broker.SendNowAsync(catalog.Slug, digest.Id))).Value();

        run.Status.Should().Be(DigestRunStatus.Running);
        run.Recipients.Single().Status.Should().Be(DigestRecipientStatusConstants.Pending);
        run.Recipients.Single().Error.Should().Be(AppNoticeErrorCodeConstants.RateLimited);
        host.Clock.Advance(TimeSpan.FromSeconds(30));
        await host.TurnAsync();
        (await host.StoreAsync(db => db.DigestDeliveries.SingleAsync(row => row.RunId == run.Id))).Status.Should().Be("pending");

        host.Clock.Advance(TimeSpan.FromSeconds(31));
        await host.TurnAsync();

        var after = (await host.DigestsAsync(broker => broker.ListRunsAsync(catalog.Slug, digest.Id, new DigestRunQuery()))).Value().Items.Single();
        after.Status.Should().Be(DigestRunStatus.Completed);
        after.Recipients.Single().Status.Should().Be(DigestRecipientStatusConstants.Sent);
        after.Recipients.Single().Attempts.Should().Be(2);
        notices.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Scheduler_ShouldRecordAnOccurrenceMissed_WhenTheHostWasDownPastTheCatchUpWindow()
    {
        var notices = new FakeAppNoticeBroker();
        var person = Linked(notices);
        var organization = Guid.NewGuid().ToString("D");
        await using var host = fixture.StartHost(new FakeTimeProvider(Sunday), notices);
        await host.DisableOtherDigestsAsync();
        var catalog = await host.CatalogAsync(organization);
        await host.EventAsync(catalog, Facts("Late fair", "2026-12-10"));
        host.Principal.ActAsMember(organization);
        var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Weekly(person)))).Value();

        host.Clock.SetUtcNow(MondayAt0400Utc.AddHours(7));
        (await host.TurnAsync()).Should().Be(0);

        var run = (await host.RunsAsync(digest.Id)).Single();
        run.Status.Should().Be("missed");
        run.OccurrenceAt.Should().Be(MondayAt0400Utc);
        notices.Sent.Should().BeEmpty();
    }

    private static string Linked(FakeAppNoticeBroker notices)
    {
        var person = Guid.NewGuid().ToString("D");
        notices.Link(person);
        return person;
    }

    private static DigestSpec Weekly(params string[] recipients) => new()
    {
        Name = "Ocharo weekly",
        Channel = DigestChannelConstants.Telegram,
        Schedule = new DigestSchedule { Days = [DayOfWeek.Monday], Times = ["09:00"], TimeZone = "Asia/Tashkent" },
        Selection = new DigestSelection { NewSinceLastRun = true, StartingWithinDays = 30, DeadlineWithinDays = 14 },
        Recipients = recipients.Select(subject => new DigestRecipient { Subject = subject }).ToList(),
        Language = DigestLanguageConstants.UzbekLatin,
    };

    internal static EventFacts Facts(string title, string starts) => new()
    {
        Title = new Dictionary<string, string> { ["en"] = title },
        Starts = new EventWhen { Value = starts, Precision = DatePrecision.Day },
        Place = new EventPlace { City = "Tashkent", Country = "UZ" },
    };
}
