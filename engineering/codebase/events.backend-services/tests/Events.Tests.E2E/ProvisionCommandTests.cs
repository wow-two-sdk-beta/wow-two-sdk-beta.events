using AwesomeAssertions;
using Events.Api.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
