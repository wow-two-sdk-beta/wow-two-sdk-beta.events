using System.Net;
using System.Text;
using AwesomeAssertions;
using Events.Api.IntakeCheck;
using Microsoft.Extensions.Configuration;

namespace Events.Tests.E2E;

/// <summary>The image's <c>intake check</c> verb over stubbed SCFD and token answers: the lines it prints, the expectations
/// it holds each answer to, and what it never prints. No database, no host.</summary>
public sealed class IntakeCheckTests
{
    private const string BaseUrl = "https://scfd.secret-host.test";
    private const string TokenEndpoint = "https://identity.secret-host.test/connect/token";
    private const string Token = "token-value-never-printed";
    private const string Subscription = "0193f3a4-0000-7000-8000-000000000001";
    private const string ForeignSubscription = "0193f3a4-0000-7000-8000-000000000002";

    [Fact]
    public async Task Check_ShouldPassAndCount_WhenEveryAnswerMatches()
    {
        var (exit, lines) = await RunAsync(Stub(), "--foreign-subscription", ForeignSubscription);

        lines.Should().Equal(
            "intake-check source=ocharo check=pull status=200 expected=200 ok=true records=3 event-records=2",
            "intake-check source=ocharo check=subscription status=200 expected=200 ok=true",
            "intake-check source=ocharo check=tenants status=403 expected=403 ok=true",
            "intake-check source=ocharo check=foreign status=404 expected=404 ok=true",
            "intake-check-result ok=true source=ocharo token=ok pull=200 records=3 event-records=2 subscription=ok tenants=403 foreign=404");
        exit.Should().Be(0);
    }

    /// <summary>Under automatic review a subscription without the source's filter would publish everything it delivers
    /// (the services sweep's safety gap): the check names the difference, never the values.</summary>
    [Theory]
    [InlineData("another-filter", "wow2.event.v1", "active", "filter-mismatch")]
    [InlineData("ocharo-fashion-v1", "wow2.post.v1", "active", "schema-mismatch")]
    [InlineData("ocharo-fashion-v1", "wow2.event.v1", "suspended", "state-suspended")]
    public async Task Check_ShouldFail_WhenTheSubscriptionDoesNotMatchTheSource(string filterKey, string schema, string state, string mismatch)
    {
        var (exit, lines) = await RunAsync(Stub(subscription: (filterKey, schema, state)));

        lines.Should().Contain($"intake-check source=ocharo check=subscription status=200 expected=200 ok=false reason={mismatch}");
        lines[^1].Should().Be($"intake-check-result ok=false source=ocharo token=ok pull=200 records=3 event-records=2 subscription={mismatch} tenants=403 foreign=skipped");
        exit.Should().Be(1);
    }

    [Fact]
    public async Task Check_ShouldFail_WhenTheTokenReachesTheTenants()
    {
        var (exit, lines) = await RunAsync(Stub(tenants: HttpStatusCode.OK));

        lines.Should().Contain("intake-check source=ocharo check=tenants status=200 expected=403 ok=false");
        lines[^1].Should().Be("intake-check-result ok=false source=ocharo token=ok pull=200 records=3 event-records=2 subscription=ok tenants=200 foreign=skipped");
        exit.Should().Be(1);
    }

    /// <summary>Identity names exactly one resource per service token: a request for another target is refused
    /// <c>invalid_target</c>, and the last line says so (events-dev, 2026-11-10: the verb sent no resource).</summary>
    [Fact]
    public async Task Check_ShouldNameTheTokenError_WhenIdentityRefusesTheTarget()
    {
        var settings = Settings();
        settings["Events:Intake:Sources:0:Resource"] = "https://another-resource.test/";

        var (exit, lines) = await RunAsync(Stub(), settings, []);

        lines[^1].Should().Be("intake-check-result ok=false source=ocharo token=token-refused:invalid_target pull=0 records=0 event-records=0 subscription=token-refused:invalid_target tenants=0 foreign=skipped");
        exit.Should().Be(1);
    }

    [Fact]
    public async Task Check_ShouldFail_WhenAForeignSubscriptionAnswers403()
    {
        var (exit, lines) = await RunAsync(Stub(foreign: HttpStatusCode.Forbidden), "--foreign-subscription", ForeignSubscription);

        lines.Should().Contain("intake-check source=ocharo check=foreign status=403 expected=404 ok=false");
        exit.Should().Be(1);
    }

    [Fact]
    public async Task Check_ShouldReportNotConfigured_WhenASourceLacksItsSettings()
    {
        var (exit, lines) = await RunAsync(Stub(), settings: new Dictionary<string, string?>
        {
            ["Events:Intake:Sources:0:Name"] = "ocharo",
            ["Events:Intake:Sources:0:SubscriptionId"] = Subscription,
        }, args: []);

        lines.Should().Equal(
            "intake-check source=ocharo ok=false reason=not-configured",
            "intake-check-result ok=false source=ocharo token=not-configured pull=0 records=0 event-records=0 subscription=0 tenants=0 foreign=skipped");
        exit.Should().Be(1);
    }

    [Fact]
    public async Task Check_ShouldOnlyCheckTheNamedSource()
    {
        var settings = Settings();
        settings["Events:Intake:Sources:1:Name"] = "other";
        settings["Events:Intake:Sources:1:SubscriptionId"] = Subscription;

        var (exit, lines) = await RunAsync(Stub(), settings: settings, args: ["--source", "ocharo"]);

        lines.Should().NotContain(line => line.Contains("source=other", StringComparison.Ordinal));
        exit.Should().Be(0);
    }

    [Fact]
    public async Task Check_ShouldRefuse_AnUnknownArgument()
    {
        var (exit, lines) = await RunAsync(Stub(), "--verbose");

        lines.Should().Equal("intake-check-result ok=false reason=usage");
        exit.Should().Be(1);
    }

    [Fact]
    public async Task Check_ShouldNeverPrint_ATokenOrAHost()
    {
        var (_, lines) = await RunAsync(Stub(), "--foreign-subscription", ForeignSubscription);

        var output = string.Join('\n', lines);
        output.Should().NotContain(Token).And.NotContain("secret-host").And.NotContain("Bearer").And.NotContain("wow2.event.v1");
    }

    [Fact]
    public void Line_ShouldFormatAnAnswerlessCheck_WithItsReason()
    {
        IntakeCheckLine.Check("ocharo", IntakeCheckLine.Pull, null, HttpStatusCode.OK, reason: "unreachable")
            .Should().Be("intake-check source=ocharo check=pull status=0 expected=200 ok=false reason=unreachable");
    }

    private static Task<(int Exit, string[] Lines)> RunAsync(Func<HttpMessageHandler> handler, params string[] args) =>
        RunAsync(handler, Settings(), args);

    private static async Task<(int Exit, string[] Lines)> RunAsync(Func<HttpMessageHandler> handler, Dictionary<string, string?> settings, string[] args)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var output = new StringWriter();
        var exit = await IntakeCheckCommand.RunAsync(args, configuration, output, handler);
        return (exit, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    private static Dictionary<string, string?> Settings() => new()
    {
        ["Events:Intake:Sources:0:Name"] = "ocharo",
        ["Events:Intake:Sources:0:Catalog"] = "ocharo-fashion",
        ["Events:Intake:Sources:0:BaseUrl"] = BaseUrl + "/",
        ["Events:Intake:Sources:0:SubscriptionId"] = Subscription,
        ["Events:Intake:Sources:0:TokenEndpoint"] = TokenEndpoint,
        ["Events:Intake:Sources:0:ClientId"] = "events",
        ["Events:Intake:Sources:0:ClientSecret"] = "client-secret-never-printed",
        ["Events:Intake:Sources:0:Scope"] = "harvest.events-read",
        ["Events:Intake:Sources:0:Filter"] = "ocharo-fashion-v1",
    };

    /// <summary>Answers as Identity's token endpoint and SCFD would; a call without the token answers 401.</summary>
    private static Func<HttpMessageHandler> Stub(
        HttpStatusCode tenants = HttpStatusCode.Forbidden, HttpStatusCode foreign = HttpStatusCode.NotFound,
        (string FilterKey, string Schema, string State)? subscription = null) =>
        () => new StubHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == TokenEndpoint)
            {
                // As Identity: a service token names exactly one resource, here SCFD's base URL with its slash.
                var form = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return form.Contains("resource=" + Uri.EscapeDataString(BaseUrl + "/"), StringComparison.Ordinal)
                    ? Answer(HttpStatusCode.OK, $$"""{"access_token":"{{Token}}","token_type":"Bearer","expires_in":300}""")
                    : Answer(HttpStatusCode.BadRequest, """{"error":"invalid_target","error_description":"Name exactly one resource."}""");
            }
            if (request.Headers.Authorization?.Parameter != Token)
                return Answer(HttpStatusCode.Unauthorized, "{}");
            if (url.StartsWith($"{BaseUrl}/api/subscriptions/{Subscription}/events", StringComparison.Ordinal))
            {
                return Answer(HttpStatusCode.OK, """
                    {"data":{"data":[
                      {"eventId":"0193f3a4-0000-7000-8000-00000000000a","data":{"schema":"wow2.event.v1"}},
                      {"eventId":"0193f3a4-0000-7000-8000-00000000000b","data":{"schema":"wow2.event.v1"}},
                      {"eventId":"0193f3a4-0000-7000-8000-00000000000c","data":{"schema":"wow2.post.v1"}}
                    ],"next":null,"hasMore":false}}
                    """);
            }

            if (url == $"{BaseUrl}/api/subscriptions/{Subscription}")
            {
                var (filterKey, schema, state) = subscription ?? ("ocharo-fashion-v1", "wow2.event.v1", "active");
                return Answer(HttpStatusCode.OK, $$$"""{"data":{"id":"{{{Subscription}}}","filterKey":"{{{filterKey}}}","schema":"{{{schema}}}","state":"{{{state}}}"}}""");
            }

            if (url == $"{BaseUrl}/api/tenants")
                return Answer(tenants, "{}");
            if (url.StartsWith($"{BaseUrl}/api/subscriptions/{ForeignSubscription}/events", StringComparison.Ordinal))
                return Answer(foreign, "{}");
            return Answer(HttpStatusCode.NotFound, "{}");
        });

    private static HttpResponseMessage Answer(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
