using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

/// <summary>The contract of <see cref="IEventWorkflowsBroker"/>: one workflow per organization, the default until it writes
/// its own, replaced at the revision read.</summary>
public abstract class EventWorkflowsBrokerConformance : EventsConformance
{
    /// <summary>A new organization reads the default workflow.</summary>
    [Fact]
    public async Task GetAsync_ShouldReturnTheDefault_ForANewOrganization()
    {
        var workflow = Value(await As(NewMember()).Workflows.GetAsync());

        workflow.Stages.Select(stage => stage.Key).Should().Equal("interested", "planning", "going", "attended", "skipped");
        workflow.Stages.Single(stage => stage.Key == "attended").Class.Should().Be(StageClass.Won);
        workflow.Stages.Single(stage => stage.Key == "skipped").Class.Should().Be(StageClass.Lost);
        workflow.DefaultStage.Should().Be("interested");
    }

    /// <summary>A put at the revision read replaces the workflow.</summary>
    [Fact]
    public async Task PutAsync_ShouldReplaceTheWorkflow_WhenTheRevisionMatches()
    {
        var member = NewMember();
        var current = Value(await As(member).Workflows.GetAsync());

        var replaced = Value(await As(member).Workflows.PutAsync(Spec(current.Revision)));

        replaced.Stages.Select(stage => stage.Key).Should().Equal("shortlisted", "applied", "won", "lost");
        replaced.DefaultStage.Should().Be("shortlisted");
        replaced.Revision.Should().BeGreaterThan(current.Revision);
        Value(await As(member).Workflows.GetAsync()).Should().BeEquivalentTo(replaced);
    }

    /// <summary>A put at a stale revision conflicts.</summary>
    [Fact]
    public async Task PutAsync_ShouldFailConflict_WhenTheRevisionIsStale()
    {
        var member = NewMember();
        var current = Value(await As(member).Workflows.GetAsync());
        Value(await As(member).Workflows.PutAsync(Spec(current.Revision)));

        Error(await As(member).Workflows.PutAsync(Spec(current.Revision))).Type.Should().Be(AppErrorType.Conflict);
    }

    /// <summary>A default stage outside the stages is invalid.</summary>
    [Fact]
    public async Task PutAsync_ShouldFailValidation_WhenTheDefaultStageIsUnknown()
    {
        var member = NewMember();
        var current = Value(await As(member).Workflows.GetAsync());

        Error(await As(member).Workflows.PutAsync(Spec(current.Revision) with { DefaultStage = "nowhere" })).Type.Should().Be(AppErrorType.Validation);
    }

    /// <summary>One organization's workflow never reaches another's.</summary>
    [Fact]
    public async Task Workflows_ShouldBeKeptPerOrganization()
    {
        var member = NewMember();
        var current = Value(await As(member).Workflows.GetAsync());
        Value(await As(member).Workflows.PutAsync(Spec(current.Revision)));

        Value(await As(NewMember()).Workflows.GetAsync()).DefaultStage.Should().Be("interested");
    }

    /// <summary>A caller without an organization has no workflow.</summary>
    [Fact]
    public async Task GetAsync_ShouldFailForbidden_WithoutAnOrganization()
    {
        Error(await As(EventsActor.Service()).Workflows.GetAsync()).Type.Should().Be(AppErrorType.Forbidden);
    }

    private static WorkflowSpec Spec(int expectedRevision) => new()
    {
        Stages =
        [
            new WorkflowStageModel { Key = "shortlisted", Label = new Dictionary<string, string> { ["en"] = "Shortlisted" }, Class = StageClass.Open },
            new WorkflowStageModel { Key = "applied", Class = StageClass.Active },
            new WorkflowStageModel { Key = "won", Class = StageClass.Won },
            new WorkflowStageModel { Key = "lost", Class = StageClass.Lost },
        ],
        DefaultStage = "shortlisted",
        ExpectedRevision = expectedRevision,
    };
}
