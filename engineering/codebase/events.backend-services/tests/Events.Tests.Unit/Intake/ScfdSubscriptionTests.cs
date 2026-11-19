using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Unit.Intake;

/// <summary>What intake checks of an SCFD subscription before it pulls: a source's filter is the subscription's
/// <c>filterKey</c>, or <c>selector:{name}</c> for the selector subscription of that name (SCFD's subscription
/// selectors).</summary>
public sealed class ScfdSubscriptionTests
{
    private const string Schema = EventsConstants.EventSchema;
    private const string Active = ScfdSubscription.ActiveState;

    [Fact]
    public void MismatchWith_ShouldAcceptASelector_WhenTheFilterNamesIt()
    {
        var subscription = new ScfdSubscription(null, Schema, Active, "selector", "ocharo-events");

        subscription.MismatchWith(Source("selector:ocharo-events")).Should().BeNull("the selector carries no filterKey; its name is the filter");
        subscription.MismatchWith(Source(" selector:ocharo-events ")).Should().BeNull("the source's filter is trimmed");
    }

    [Theory]
    [InlineData("selector:ocharo-fashion")]
    [InlineData("selector:Ocharo-Events")]
    [InlineData("selector:")]
    [InlineData("ocharo-events")]
    public void MismatchWith_ShouldRefuseAnotherSelectorName(string filter)
    {
        var subscription = new ScfdSubscription(null, Schema, Active, "selector", "ocharo-events");

        subscription.MismatchWith(Source(filter)).Should().Be("filter-mismatch");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("ocharo-fashion-v1", null)]
    [InlineData("selector:ocharo-events", null)]
    [InlineData(null, "ocharo-events")]
    public void MismatchWith_ShouldRefuseAChannel_WhenTheFilterNamesASelector(string? filterKey, string? name)
    {
        var channel = new ScfdSubscription(filterKey, Schema, Active, Kind: null, Name: name);

        channel.MismatchWith(Source("selector:ocharo-events")).Should().Be("filter-mismatch", "only a subscription of kind selector answers to a selector name");
    }

    [Fact]
    public void MismatchWith_ShouldRefuseASelector_WhenTheSourceNamesAFilter()
    {
        var subscription = new ScfdSubscription(null, Schema, Active, "selector", "ocharo-events");

        subscription.MismatchWith(Source("ocharo-fashion-v1")).Should().Be("filter-mismatch", "a selector has no filterKey to carry the source's filter");
        subscription.MismatchWith(Source(null)).Should().BeNull("a source naming no filter accepts any subscription");
    }

    [Theory]
    [InlineData("wow2.post.v1", "active", "schema-mismatch")]
    [InlineData(Schema, "suspended", "state-suspended")]
    [InlineData(Schema, "archived", "state-archived")]
    public void MismatchWith_ShouldCheckSchemaAndStateFirst_ForASelector(string schema, string state, string mismatch)
    {
        new ScfdSubscription(null, schema, state, "selector", "ocharo-events").MismatchWith(Source("selector:ocharo-events")).Should().Be(mismatch);
    }

    [Fact]
    public void MismatchWith_ShouldKeepReadingAFilterAsBefore_ForAChannel()
    {
        var channel = new ScfdSubscription("ocharo-fashion-v1", Schema, Active);

        channel.MismatchWith(Source("ocharo-fashion-v1")).Should().BeNull();
        channel.MismatchWith(Source("another-filter-v1")).Should().Be("filter-mismatch");
    }

    [Theory]
    [InlineData("""{"kind":"selector","name":"ocharo-events","schema":"wow2.event.v1","state":"active"}""")]
    [InlineData("""{"kind":"Selector","name":"ocharo-events","schema":"wow2.event.v1","state":"active"}""")]
    [InlineData("""{"kind":3,"name":"ocharo-events","schema":"wow2.event.v1","state":0}""")]
    public void Read_ShouldTakeASelectorsKindAndName_AsALabelOrTheNumber(string json)
    {
        var subscription = Read(json);

        subscription.Should().Be(new ScfdSubscription(null, Schema, Active, "selector", "ocharo-events"));
        subscription.MismatchWith(Source("selector:ocharo-events")).Should().BeNull();
    }

    [Theory]
    [InlineData("""{"kind":"channel","filterKey":"ocharo-fashion-v1","schema":"wow2.event.v1","state":"active"}""")]
    [InlineData("""{"kind":0,"filterKey":"ocharo-fashion-v1","schema":"wow2.event.v1","state":"active"}""")]
    [InlineData("""{"filterKey":"ocharo-fashion-v1","schema":"wow2.event.v1","state":"active"}""")]
    public void Read_ShouldReadAChannelAsBefore_WithNoKindAndNoName(string json)
    {
        Read(json).Should().Be(new ScfdSubscription("ocharo-fashion-v1", Schema, Active));
    }

    [Fact]
    public void Describe_ShouldNameKindAndName_WithoutAnythingUnsafe()
    {
        var selector = new ScfdSubscription(null, Schema, Active, "selector", "ocharo-events");
        var channel = new ScfdSubscription("ocharo-fashion-v1", Schema, Active);

        selector.Describe(Source("selector:ocharo-events")).Should().Contain("expects filter 'selector:ocharo-events'").And.Contain("kind 'selector'").And.Contain("name 'ocharo-events'");
        channel.Describe(Source("selector:ocharo-events")).Should().Contain("kind '(not a selector)'").And.Contain("name '(none)'");
        new ScfdSubscription(null, Schema, Active, "selector", "bad name\n?token=1").Describe(Source(null)).Should().Contain("name 'badnametoken1'");
    }

    private static ScfdSubscription Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ScfdSubscription.Read(document.RootElement)!;
    }

    private static EventsIntakeSourceOptions Source(string? filter) =>
        new() { Name = "ocharo-events", Catalog = "ocharo-fashion", Filter = filter };
}
