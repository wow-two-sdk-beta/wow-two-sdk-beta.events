using System.Text.Json;
using AwesomeAssertions;
using Events.Tests.Integration.Infrastructure;
using Events.Tests.Integration.Tracking;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace Events.Tests.Integration.Workflows;

/// <summary>Covers the module's workflows: the default an organization reads, writes, revisions and stages in use.</summary>
/// <param name="fixture">The module over PostgreSQL.</param>
public sealed class WorkflowsBrokerTests(ModuleFixture fixture) : IClassFixture<ModuleFixture>
{
    [Fact]
    public async Task GetAsync_ShouldReturnTheDefaultWorkflow_WhenTheOrganizationNeverWroteOne()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        await using var scope = fixture.Scope();

        var workflow = (await Broker(scope).GetAsync()).Value();

        workflow.Revision.Should().Be(0);
        workflow.DefaultStage.Should().Be("interested");
        workflow.Stages.Select(stage => (stage.Key, stage.Class)).Should().Equal(
            ("interested", StageClass.Open), ("planning", StageClass.Active), ("going", StageClass.Active),
            ("attended", StageClass.Won), ("skipped", StageClass.Lost));
        workflow.Stages.Should().OnlyContain(stage => stage.Label.Keys.Order().SequenceEqual(new[] { "en", "ru", "uz" }));
    }

    [Fact]
    public async Task GetAsync_ShouldFailWithOrganizationRequired_WhenTheCallerHasNoOrganization()
    {
        fixture.Principal.ActAsAnonymous();
        await using var scope = fixture.Scope();

        (await Broker(scope).GetAsync()).Error().Type.Should().Be(AppErrorType.Forbidden);
    }

    [Fact]
    public async Task PutAsync_ShouldStoreTheWorkflowPerOrganization_WhenItIsValid()
    {
        var organization = ModuleFixture.NewOrganization();
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        using var schema = JsonDocument.Parse("""{ "type": "object", "properties": { "cost": { "type": "string" } } }""");

        var written = (await broker.PutAsync(OcharoWorkflow.Spec(0) with { Fields = schema.RootElement })).Value();
        var read = (await broker.GetAsync()).Value();

        written.Revision.Should().Be(1);
        read.Stages.Select(stage => stage.Key).Should().Equal(OcharoWorkflow.Keys);
        read.Stages.Single(stage => stage.Key == "rejected").Class.Should().Be(StageClass.Lost);
        read.DefaultStage.Should().Be("candidate");
        read.Fields!.Value.GetProperty("properties").GetProperty("cost").GetProperty("type").GetString().Should().Be("string");

        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        (await broker.GetAsync()).Value().Revision.Should().Be(0, "another organization still reads the default");
    }

    [Fact]
    public async Task PutAsync_ShouldFailWithRevisionConflict_WhenTheRevisionIsMissingOrStale()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        (await broker.PutAsync(OcharoWorkflow.Spec())).Value();

        (await broker.PutAsync(OcharoWorkflow.Spec())).Error().Type.Should().Be(AppErrorType.Conflict);
        (await broker.PutAsync(OcharoWorkflow.Spec(0))).Error().Type.Should().Be(AppErrorType.Conflict);
        (await broker.PutAsync(OcharoWorkflow.Spec(1))).Value().Revision.Should().Be(2);
    }

    [Fact]
    public async Task PutAsync_ShouldFailWithValidation_WhenTheStagesAreInvalid()
    {
        fixture.Principal.ActAsMember(ModuleFixture.NewOrganization());
        await using var scope = fixture.Scope();
        var broker = Broker(scope);
        var spec = OcharoWorkflow.Spec();

        (await broker.PutAsync(spec with { Stages = [] })).Error().Type.Should().Be(AppErrorType.Validation);
        (await broker.PutAsync(spec with { Stages = [.. spec.Stages, spec.Stages[0]] })).Error().Message.Should().Contain("repeats");
        (await broker.PutAsync(spec with { Stages = [new WorkflowStageModel { Key = "Not A Key" }], DefaultStage = "Not A Key" }))
            .Error().Message.Should().Contain("stages[0].key");
        (await broker.PutAsync(spec with { DefaultStage = "missing" })).Error().Message.Should().Contain("defaultStage");
    }

    [Fact]
    public async Task PutAsync_ShouldRefuseRemovingAStage_WhenTheOrganizationsTrackingStillUsesIt()
    {
        var organization = ModuleFixture.NewOrganization();
        var catalog = await CatalogSeed.CatalogAsync(fixture, organization);
        var item = await CatalogSeed.EventAsync(fixture, catalog, CatalogSeed.Facts("Tashkent Fashion Week", "2026-12-01"));
        fixture.Principal.ActAsMember(organization);
        await using var scope = fixture.Scope();
        var tracking = scope.ServiceProvider.GetRequiredService<IEventTrackingBroker>();
        (await tracking.PutAsync(catalog.Slug, item.Id, new TrackingSpec { Stage = "planning" })).Value();

        var error = (await Broker(scope).PutAsync(OcharoWorkflow.Spec(0))).Error();

        error.Type.Should().Be(AppErrorType.Validation);
        error.Message.Should().Contain("'planning'");
        (await Broker(scope).GetAsync()).Value().Revision.Should().Be(0, "nothing was written");
    }

    private static IEventWorkflowsBroker Broker(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>();
}
