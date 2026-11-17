using AwesomeAssertions;
using Events.Tests.Integration.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.Notices;

namespace Events.Tests.Integration.Digests;

/// <summary>Covers what a digest sends and to whom: new events since the last run, each recipient's Telegram result, the
/// preview's text against the sent one, a message per recipient language, the selection's rules and filters, and role
/// recipients through the host's resolver.</summary>
/// <param name="fixture">The module's database.</param>
public sealed class DigestDeliveryTests(DigestFixture fixture) : IClassFixture<DigestFixture>
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NewSinceLastRun_ShouldSkipEventsThePreviousRunSent()
    {
        var (host, person, organization) = await StartAsync();
        await using var _ = host;
        var catalog = await host.CatalogAsync(organization);
        var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Spec(person) with
        {
            Selection = new DigestSelection { NewSinceLastRun = true },
        }))).Value();
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        var first = await host.EventAsync(catalog, Facts("First fair", "2027-02-01"));
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        var firstRun = (await host.DigestsAsync(broker => broker.SendNowAsync(catalog.Slug, digest.Id))).Value();
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await host.EventAsync(catalog, Facts("Second fair", "2027-03-01"));
        host.Clock.Advance(TimeSpan.FromMinutes(1));

        var secondRun = (await host.DigestsAsync(broker => broker.SendNowAsync(catalog.Slug, digest.Id))).Value();

        firstRun.EventIds.Should().Equal(first.Id);
        secondRun.EventIds.Should().Equal(second.Id);
        host.Notices.Sent.Select(notice => notice.Text).Should().SatisfyRespectively(
            text => text.Should().Contain("First fair"),
            text => text.Should().Contain("Second fair").And.NotContain("First fair"));
    }

    [Fact]
    public async Task SendNow_ShouldRecordEachRecipientsTelegramResult()
    {
        var (host, linked, organization) = await StartAsync();
        await using var _ = host;
        var unlinked = Guid.NewGuid().ToString("D");
        var muted = Guid.NewGuid().ToString("D");
        host.Notices.Link(muted).Mute(muted);
        var catalog = await host.CatalogAsync(organization);
        await host.EventAsync(catalog, Facts("Runway night", "2026-12-01"));
        var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Spec(linked, unlinked, muted)))).Value();

        var run = (await host.DigestsAsync(broker => broker.SendNowAsync(catalog.Slug, digest.Id))).Value();

        run.Status.Should().Be(DigestRunStatus.Completed);
        run.Recipients.Select(recipient => (recipient.Subject, recipient.Status)).Should().Equal(
            (linked, DigestRecipientStatusConstants.Sent),
            (unlinked, DigestRecipientStatusConstants.NotLinked),
            (muted, DigestRecipientStatusConstants.Muted));
        host.Notices.Sent.Should().ContainSingle().Which.Subject.Should().Be(linked);
        host.Notices.Sent[0].IdempotencyKey.Should().StartWith($"events-digest:{run.Id:N}:");
    }

    [Fact]
    public async Task SendNow_ShouldReadFailed_WhenIdentityRefusesTheNotice()
    {
        var (host, person, organization) = await StartAsync();
        await using var _ = host;
        var catalog = await host.CatalogAsync(organization);
        await host.EventAsync(catalog, Facts("Blocked fair", "2026-12-01"));
        var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Spec(person)))).Value();
        host.Notices.FailNext(AppError.Of(AppErrorType.ExternalRejected, "Telegram refused.", new Dictionary<string, object?>
        {
            [AppNoticeConstants.CodeMetadataKey] = AppNoticeErrorCodeConstants.Refused,
            [AppNoticeConstants.ReasonMetadataKey] = "bot was blocked by the user",
        }));

        var run = (await host.DigestsAsync(broker => broker.SendNowAsync(catalog.Slug, digest.Id))).Value();

        run.Status.Should().Be(DigestRunStatus.Failed);
        run.Recipients.Single().Status.Should().Be(DigestRecipientStatusConstants.Failed);
        run.Recipients.Single().Error.Should().Be("NoticeRefused: bot was blocked by the user");
    }

    [Fact]
    public async Task Preview_ShouldRenderTheTextTheRunSends()
    {
        var (host, person, organization) = await StartAsync();
        await using var _ = host;
        var catalog = await host.CatalogAsync(organization, "linked-catalog-" + Guid.NewGuid().ToString("N")[..8]);
        var item = await host.EventAsync(catalog, new EventFacts
        {
            Title = new Dictionary<string, string> { ["en"] = "Tashkent <Fashion> & Design Week" },
            Starts = new EventWhen { Value = "2026-12", Precision = DatePrecision.Month, Certainty = DateCertainty.Estimated },
            Place = new EventPlace { City = "Tashkent", Country = "UZ" },
            Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = new EventWhen { Value = "2026-11-25", Precision = DatePrecision.Day } }],
        });
        var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Spec(person) with
        {
            Language = DigestLanguageConstants.English,
            LinkTemplate = "https://app.example.test/{catalog}/events/{id}",
        }))).Value();

        var preview = (await host.DigestsAsync(broker => broker.PreviewAsync(catalog.Slug, digest.Id))).Value();
        var run = (await host.DigestsAsync(broker => broker.SendNowAsync(catalog.Slug, digest.Id))).Value();

        var text = preview.Texts.Should().ContainSingle().Which.Text;
        host.Notices.Sent.Should().ContainSingle().Which.Text.Should().Be(text);
        run.Texts.Single().Text.Should().Be(text);
        text.Should().Be(
            "<b>Weekly events</b>\n18 November 2026\n" +
            $"\n1. <b><a href=\"https://app.example.test/{catalog.Slug}/events/{item.Id:D}\">Tashkent &lt;Fashion&gt; &amp; Design Week</a></b>\n" +
            "December 2026 (estimated) · Tashkent\n" +
            "Applications close: 25 November 2026\n");
    }

    [Fact]
    public async Task SendNow_ShouldRenderOneMessagePerRecipientLanguage()
    {
        var (host, uzbek, organization) = await StartAsync();
        await using var _ = host;
        var russian = Guid.NewGuid().ToString("D");
        host.Notices.Link(russian);
        var catalog = await host.CatalogAsync(organization);
        await host.EventAsync(catalog, new EventFacts
        {
            Title = new Dictionary<string, string> { ["uz"] = "Moda haftaligi", ["ru"] = "Неделя моды" },
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
        });
        var digest = (await host.DigestsAsync(broker => broker.CreateAsync(catalog.Slug, Spec(uzbek) with
        {
            Recipients = [new DigestRecipient { Subject = uzbek }, new DigestRecipient { Subject = russian, Language = DigestLanguageConstants.Russian }],
        }))).Value();

        var run = (await host.DigestsAsync(broker => broker.SendNowAsync(catalog.Slug, digest.Id))).Value();

        run.Texts.Select(text => (text.Language, text.Recipients)).Should().Equal(("uz-Latn", 1), ("ru", 1));
        var sent = host.Notices.Sent.ToDictionary(notice => notice.Subject);
        sent[uzbek].Text.Should().Contain("Moda haftaligi").And.Contain("3-dekabr 2026");
        sent[uzbek].Culture.Should().Be("uz-Latn");
        sent[russian].Text.Should().Contain("Неделя моды").And.Contain("3 декабря 2026");
        sent[russian].Culture.Should().Be("ru");
    }

    [Fact]
    public async Task Preview_ShouldSelectByRulesAndFilters_AsOfTheLocalDay()
    {
        var (host, person, organization) = await StartAsync();
        await using var _ = host;
        var catalog = await host.CatalogAsync(organization);
        var soon = await host.EventAsync(catalog, Facts("Soon runway", "2026-11-28") with { Kind = "runway" });
        var deadline = await host.EventAsync(catalog, Facts("Far fair with a deadline", "2027-03-01") with
        {
            Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = new EventWhen { Value = "2026-11-25", Precision = DatePrecision.Day } }],
        });
        await host.EventAsync(catalog, Facts("Far fair", "2027-03-01"));
        await host.EventAsync(catalog, Facts("Cancelled show", "2026-11-28") with { Status = EventStatus.Cancelled });
        await host.EventAsync(catalog, Facts("Ended show", "2026-11-10"));
        var samarkand = await host.EventAsync(catalog, Facts("Samarkand expo", "2026-11-30") with { Place = new EventPlace { City = "Samarkand" } });
        await host.EventAsync(catalog, Facts("Candidate show", "2026-11-28"), EventReviewState.Candidate);
        var rules = new DigestSelection { StartingWithinDays = 30, DeadlineWithinDays = 14, Statuses = [EventStatus.Scheduled] };

        var all = await PreviewIdsAsync(host, catalog.Slug, Spec(person) with { Selection = rules });
        var runways = await PreviewIdsAsync(host, catalog.Slug, Spec(person) with { Selection = rules with { Kinds = ["runway"] } });
        var tashkent = await PreviewIdsAsync(host, catalog.Slug, Spec(person) with { Selection = rules with { Cities = ["TASHKENT"] } });
        await host.TrackAsync(organization, samarkand.Id);
        var tracked = await PreviewIdsAsync(host, catalog.Slug, Spec(person) with { Selection = rules with { TrackedOnly = true } });
        var capped = (await host.DigestsAsync(async broker => (await broker.PreviewAsync(catalog.Slug,
            (await broker.CreateAsync(catalog.Slug, Spec(person) with { Selection = rules, ItemCap = 1, Language = DigestLanguageConstants.English })).Value().Id)).Value()));

        all.Should().Equal(soon.Id, samarkand.Id, deadline.Id);
        runways.Should().Equal(soon.Id);
        tashkent.Should().Equal(soon.Id, deadline.Id);
        tracked.Should().Equal(samarkand.Id);
        capped.Matched.Should().Be(3);
        capped.Items.Should().ContainSingle();
        capped.Texts.Single().Text.Should().EndWith("\n…and 2 more");
    }

    [Fact]
    public async Task Recipients_ShouldResolveARole_ThroughTheHostsResolver_AndNoteItWithoutOne()
    {
        var notices = new FakeAppNoticeBroker();
        var members = new[] { Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D") };
        var organization = Guid.NewGuid().ToString("D");
        var resolver = new StaticRoleResolver(members);
        DigestModel digest;
        string slug;
        await using (var unbound = fixture.StartHost(new FakeTimeProvider(Start), notices))
        {
            unbound.Principal.ActAsMember(organization);
            var catalog = await unbound.CatalogAsync(organization);
            slug = catalog.Slug;
            await unbound.EventAsync(catalog, Facts("Members' fair", "2026-12-01"));
            digest = (await unbound.DigestsAsync(broker => broker.CreateAsync(slug, Spec(members[0]) with
            {
                Recipients = [new DigestRecipient { Role = "member" }, new DigestRecipient { Subject = members[0], Language = DigestLanguageConstants.English }],
            }))).Value();

            var preview = (await unbound.DigestsAsync(broker => broker.PreviewAsync(slug, digest.Id))).Value();

            preview.Recipients.Select(recipient => recipient.Subject).Should().Equal(members[0]);
            preview.Notes.Should().ContainSingle().Which.Should().StartWith("Role 'member' could not be read");
        }

        await using var bound = fixture.StartHost(new FakeTimeProvider(Start), notices, services => services.AddScoped<IDigestRecipientResolver>(_ => resolver));
        bound.Principal.ActAsMember(organization);

        var resolved = (await bound.DigestsAsync(broker => broker.PreviewAsync(slug, digest.Id))).Value();

        resolved.Recipients.Select(recipient => (recipient.Subject, recipient.Language)).Should().Equal((members[0], "uz-Latn"), (members[1], "uz-Latn"));
        resolved.Notes.Should().BeEmpty();
        resolver.Asked.Should().Equal(("https://identity.test/", organization, "member"));
    }

    private async Task<(DigestHost Host, string Person, string Organization)> StartAsync()
    {
        var notices = new FakeAppNoticeBroker();
        var person = Guid.NewGuid().ToString("D");
        notices.Link(person);
        var organization = Guid.NewGuid().ToString("D");
        var host = fixture.StartHost(new FakeTimeProvider(Start), notices);
        host.Principal.ActAsMember(organization);
        await Task.CompletedTask;
        return (host, person, organization);
    }

    private static async Task<List<Guid>> PreviewIdsAsync(DigestHost host, string catalog, DigestSpec spec) =>
        await host.DigestsAsync(async broker =>
        {
            var digest = (await broker.CreateAsync(catalog, spec)).Value();
            return (await broker.PreviewAsync(catalog, digest.Id)).Value().Items.Select(item => item.EventId).ToList();
        });

    private static DigestSpec Spec(params string[] recipients) => new()
    {
        Name = "Weekly events",
        Channel = DigestChannelConstants.Telegram,
        Enabled = false,
        Schedule = new DigestSchedule { Days = [DayOfWeek.Monday], Times = ["09:00"], TimeZone = "Asia/Tashkent" },
        Selection = new DigestSelection { StartingWithinDays = 30 },
        Recipients = recipients.Select(subject => new DigestRecipient { Subject = subject }).ToList(),
        Language = DigestLanguageConstants.UzbekLatin,
    };

    private static EventFacts Facts(string title, string starts) => DigestSchedulerTests.Facts(title, starts);

    private sealed class StaticRoleResolver(IReadOnlyList<string> members) : IDigestRecipientResolver
    {
        public List<(string Issuer, string Organization, string Role)> Asked { get; } = [];

        public Task<Result<IReadOnlyList<string>>> ResolveRoleAsync(string issuer, string organizationId, string role, CancellationToken cancellationToken = default)
        {
            Asked.Add((issuer, organizationId, role));
            return Task.FromResult(Result<IReadOnlyList<string>>.Ok(members));
        }
    }
}
