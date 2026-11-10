using AwesomeAssertions;
using Events.Api.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace Events.Tests.E2E;

/// <summary>The deploy's operator verbs: <c>catalog ensure</c> and <c>workflow put</c> provision the owner organization's
/// catalog and workflow before anyone signs in, idempotently, over the migrated database.</summary>
public sealed class ProvisionCommandTests : IAsyncLifetime
{
    private const string Issuer = "https://identity.ocharo.test/";
    private const string Ocharo = "org-ocharo";
    private const string DeskStages = "candidate:open,shortlisted:open,applying:active,applied:active,confirmed:active,attended:won,skipped:lost,rejected:lost";

    private readonly PostgresFixture postgres = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());

    [Fact]
    public async Task CatalogEnsure_ShouldCreateAPrivateCatalog_ThenChangeNothingOnARerun()
    {
        var first = await RunAsync("catalog", "ensure", "ocharo-fashion", "--owner-org", Ocharo, "--name", "Ocharo fashion", "--kinds", "runway,trade-fair");
        var second = await RunAsync("catalog", "ensure", "ocharo-fashion", "--owner-org", Ocharo, "--name", "Ocharo fashion", "--kinds", "runway,trade-fair");

        first.Should().Be((0, "catalog-ensure-result ok=true slug=ocharo-fashion created=true changed=true revision=1"));
        second.Should().Be((0, "catalog-ensure-result ok=true slug=ocharo-fashion created=false changed=false revision=1"));
        var catalog = Value(await (await AsOwner()).GetRequiredService<IEventCatalogsBroker>().GetAsync("ocharo-fashion"));
        catalog.Visibility.Should().Be(CatalogVisibility.Private);
        catalog.OwnerOrganizationId.Should().Be(Ocharo);
        catalog.Kinds.Select(kind => kind.Key).Should().Equal("runway", "trade-fair");
    }

    [Fact]
    public async Task CatalogEnsure_ShouldRefuse_WhenAnotherOrganizationOwnsTheSlug()
    {
        await RunAsync("catalog", "ensure", "taken", "--owner-org", Ocharo);

        var (exit, line) = await RunAsync("catalog", "ensure", "taken", "--owner-org", "org-other");

        exit.Should().Be(1);
        line.Should().StartWith("catalog-ensure-result ok=false slug=taken reason=");
    }

    [Fact]
    public async Task WorkflowPut_ShouldWriteDesksStages_ThenChangeNothingOnARerun()
    {
        var first = await RunAsync("workflow", "put", "--owner-org", Ocharo, "--default", "candidate", "--stages", DeskStages);
        var second = await RunAsync("workflow", "put", "--owner-org", Ocharo, "--default", "candidate", "--stages", DeskStages);

        first.Should().Be((0, "workflow-put-result ok=true stages=8 changed=true revision=1"));
        second.Should().Be((0, "workflow-put-result ok=true stages=8 changed=false revision=1"));
        var workflow = Value(await (await AsOwner()).GetRequiredService<IEventWorkflowsBroker>().GetAsync());
        workflow.Stages.Single(stage => stage.Key == "attended").Class.Should().Be(StageClass.Won);
    }

    private const string Bundle = """
        {"format":"wow2.events-import.v1","source":"ocharo-desk","rows":[
          {"legacyId":"0193f3a4-0000-7000-8000-0000000000a1",
           "facts":{"key":"tashkent-fashion-week-2026","title":{"und":"Tashkent Fashion Week"},"kind":"runway",
                    "starts":{"value":"2026-10","precision":"month","certainty":"estimated"}},
           "tracking":{"stage":"candidate","priority":"high","notes":"Fit for the SS27 line.","fields":{"participation":"apply","cost":"unknown"}},
           "legacy":{"id":"0193f3a4-0000-7000-8000-0000000000a1","status":"candidate"}},
          {"legacyId":"0193f3a4-0000-7000-8000-0000000000a2",
           "facts":{"title":{"und":"Craft fair, dates not announced"},"kind":"market","starts":{"precision":"unknown"}},
           "tracking":{"stage":"skipped","notes":"Too far."}}]}
        """;

    /// <summary>An operator imports a tenant's own bundle from standard input: a dry run first, then the import, then a
    /// rerun that finds every row through the id map and changes nothing.</summary>
    [Fact]
    public async Task Import_ShouldDryRunImportAndRerun_FromStandardInput()
    {
        await RunAsync("catalog", "ensure", "ocharo-fashion", "--owner-org", Ocharo, "--kinds", "runway,market,other");
        await RunAsync("workflow", "put", "--owner-org", Ocharo, "--default", "candidate", "--stages", DeskStages);

        var dry = await ImportAsync(Bundle, "import", "ocharo-fashion", "--owner-org", Ocharo, "--dry-run");
        var real = await ImportAsync(Bundle, "import", "ocharo-fashion", "--owner-org", Ocharo);
        var again = await ImportAsync(Bundle, "import", "ocharo-fashion", "--owner-org", Ocharo);

        dry.Exit.Should().Be(0);
        dry.Last.Should().Be("import-result ok=true dry-run=true rows=2 created=2 updated=0 unchanged=0 failed=0");
        real.Last.Should().Be("import-result ok=true dry-run=false rows=2 created=2 updated=0 unchanged=0 failed=0");
        again.Last.Should().Be("import-result ok=true dry-run=false rows=2 created=0 updated=0 unchanged=2 failed=0");
        Map(again.Report).Should().Equal(Map(real.Report), "the id map is stable on every rerun");
    }

    [Fact]
    public async Task Import_ShouldRefuse_ABundleThatIsNotJson()
    {
        var (exit, _, last) = await ImportAsync("not json", "import", "ocharo-fashion", "--owner-org", Ocharo);

        exit.Should().Be(1);
        last.Should().Be("import-result ok=false reason=bundle-not-json");
    }

    /// <summary>An operator reads a stored event back, in one line, by its id, its facts' key or an SCFD post among its
    /// sources: Wheelhouse's check of what intake or an import stored.</summary>
    [Fact]
    public async Task EventShow_ShouldPrintTheStoredEvent_ByItsIdKeyOrPost()
    {
        await RunAsync("catalog", "ensure", "ocharo-fashion", "--owner-org", Ocharo, "--kinds", "runway,market,other");
        await RunAsync("workflow", "put", "--owner-org", Ocharo, "--default", "candidate", "--stages", DeskStages);
        var imported = await ImportAsync(Bundle, "import", "ocharo-fashion", "--owner-org", Ocharo);
        var id = Map(imported.Report).Single(entry => entry.StartsWith("0193f3a4-0000-7000-8000-0000000000a1=", StringComparison.Ordinal)).Split('=')[1];
        var post = Guid.NewGuid();
        await SqlAsync(
            $"INSERT INTO {EventsModuleConstants.SchemaName}.events_event_sources (catalog_id, kind, source_id, event_id, first_seen_at, last_seen_at) " +
            $"SELECT catalog_id, 'scfd', 'scfd/{post:D}', id, now(), now() FROM {EventsModuleConstants.SchemaName}.events_catalog_events WHERE id = '{id}'");

        var byId = await ShowAsync("event", "show", "ocharo-fashion", "--owner-org", Ocharo, "--id", id);
        var byKey = await ShowAsync("event", "show", "ocharo-fashion", "--owner-org", Ocharo, "--key", "tashkent-fashion-week-2026");
        var byPost = await ShowAsync("event", "show", "ocharo-fashion", "--owner-org", Ocharo, "--post", post.ToString());

        byId.Exit.Should().Be(0);
        byId.Last.Should().StartWith($"event-show-result ok=true id={id} review=published revision=");
        byId.Last.Should().Contain("facts={").And.Contain("\"key\":\"tashkent-fashion-week-2026\"");
        byKey.Should().Be(byId);
        byPost.Should().Be(byId);
    }

    [Fact]
    public async Task EventShow_ShouldShowNothing_ToAnotherOrganizationOrForAMissingEvent()
    {
        await RunAsync("catalog", "ensure", "ocharo-fashion", "--owner-org", Ocharo, "--kinds", "runway,market,other");
        await RunAsync("workflow", "put", "--owner-org", Ocharo, "--default", "candidate", "--stages", DeskStages);
        await ImportAsync(Bundle, "import", "ocharo-fashion", "--owner-org", Ocharo);

        var foreign = await ShowAsync("event", "show", "ocharo-fashion", "--owner-org", "org-other", "--key", "tashkent-fashion-week-2026");
        var missing = await ShowAsync("event", "show", "ocharo-fashion", "--owner-org", Ocharo, "--key", "no-such-event");

        foreign.Exit.Should().Be(1);
        foreign.Last.Should().StartWith("event-show-result ok=false reason=").And.NotContain("tashkent");
        missing.Should().Be((1, "event-show-result ok=false reason=the-event-was-not-found"));
    }

    [Theory]
    [InlineData("event", "show", "--owner-org", Ocharo, "--key", "a")]
    [InlineData("event", "show", "ocharo-fashion", "--owner-org", Ocharo)]
    [InlineData("event", "show", "ocharo-fashion", "--owner-org", Ocharo, "--id", "not-a-guid")]
    [InlineData("event", "show", "ocharo-fashion", "--owner-org", Ocharo, "--key", "a", "--id", "0193f3a4-0000-7000-8000-0000000000a1")]
    [InlineData("event", "show", "ocharo-fashion", "--key", "a")]
    public async Task EventShow_ShouldRefuseIncompleteArguments(params string[] args)
    {
        (await ShowAsync(args)).Should().Be((1, "event-show-result ok=false reason=usage"));
    }

    [Theory]
    [InlineData("catalog", "ensure")]
    [InlineData("catalog", "ensure", "ocharo-fashion")]
    [InlineData("workflow", "put", "--owner-org", Ocharo, "--stages", "candidate:open")]
    [InlineData("catalog", "ensure", "ocharo-fashion", "--owner-org", Ocharo, "--unknown", "x")]
    public async Task Verbs_ShouldRefuseIncompleteArguments(params string[] args)
    {
        (await RunAsync(args)).Exit.Should().Be(1);
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        // The deploy's migrate step, which runs before these verbs.
        await using var provider = Module(new ServiceCollection());
        (await provider.MigrateBespokeForDeployAsync()).ValueOrThrow();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await postgres.DisposeAsync();

    private async Task<(int Exit, string Line)> RunAsync(params string[] args)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseSettings:ConnectionString"] = postgres.ConnectionString,
            ["Events:Auth:Issuers:0:Issuer"] = Issuer,
        }).Build();
        using var output = new StringWriter();
        var exit = await ProvisionCommand.RunAsync(args, configuration, output);
        return (exit, output.ToString().Trim());
    }

    private async Task<(int Exit, string Report, string Last)> ImportAsync(string bundle, params string[] args)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseSettings:ConnectionString"] = postgres.ConnectionString,
            ["Events:Auth:Issuers:0:Issuer"] = Issuer,
        }).Build();
        using var input = new StringReader(bundle);
        using var output = new StringWriter();
        var exit = await ImportCommand.RunAsync(args, configuration, input, output);
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return (exit, lines.FirstOrDefault(line => line.StartsWith("import-report ", StringComparison.Ordinal)) ?? string.Empty, lines[^1]);
    }

    private async Task<(int Exit, string Last)> ShowAsync(params string[] args)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseSettings:ConnectionString"] = postgres.ConnectionString,
            ["Events:Auth:Issuers:0:Issuer"] = Issuer,
        }).Build();
        using var output = new StringWriter();
        var exit = await EventShowCommand.RunAsync(args, configuration, output);
        return (exit, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[^1]);
    }

    private async Task SqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static IEnumerable<string> Map(string reportLine)
    {
        using var document = System.Text.Json.JsonDocument.Parse(reportLine["import-report ".Length..]);
        return [.. document.RootElement.GetProperty("map").EnumerateArray()
            .Select(entry => $"{entry.GetProperty("legacyId").GetString()}={entry.GetProperty("eventId").GetString()}")];
    }

    private async Task<IServiceProvider> AsOwner()
    {
        var services = new ServiceCollection().AddSingleton<ICurrentPrincipal>(new OperatorPrincipal(Issuer, Ocharo));
        var provider = Module(services);
        await Task.CompletedTask;
        return provider.CreateScope().ServiceProvider;
    }

    private ServiceProvider Module(IServiceCollection services)
    {
        services.AddLogging();
        services.AddEvents(EventsModuleConstants.ServiceSchemaOwner).Configure(options => options.ConnectionString = postgres.ConnectionString);
        return services.BuildServiceProvider();
    }

    private static T Value<T>(Result<T> result) =>
        result is Result<T>.Success success ? success.Value : throw new Xunit.Sdk.XunitException(((Result<T>.Failure)result).Error.Message);
}
