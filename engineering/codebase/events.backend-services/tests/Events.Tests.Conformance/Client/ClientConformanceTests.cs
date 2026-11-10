using WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

namespace Events.Tests.Conformance.Client;

/// <summary>Forms 2 and 3: the catalogs suite through the client against the service host.</summary>
[Collection(ClientConformanceCollection.Name)]
public sealed class ClientCatalogsConformanceTests(ClientEventsFixture fixture) : EventCatalogsBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Forms 2 and 3: the catalog events suite through the client against the service host.</summary>
[Collection(ClientConformanceCollection.Name)]
public sealed class ClientCatalogEventsConformanceTests(ClientEventsFixture fixture) : CatalogEventsBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Forms 2 and 3: the review suite through the client against the service host.</summary>
[Collection(ClientConformanceCollection.Name)]
public sealed class ClientReviewConformanceTests(ClientEventsFixture fixture) : EventReviewBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Forms 2 and 3: the tracking suite through the client against the service host.</summary>
[Collection(ClientConformanceCollection.Name)]
public sealed class ClientTrackingConformanceTests(ClientEventsFixture fixture) : EventTrackingBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Forms 2 and 3: the workflows suite through the client against the service host.</summary>
[Collection(ClientConformanceCollection.Name)]
public sealed class ClientWorkflowsConformanceTests(ClientEventsFixture fixture) : EventWorkflowsBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Forms 2 and 3: the imports suite through the client against the service host.</summary>
[Collection(ClientConformanceCollection.Name)]
public sealed class ClientImportsConformanceTests(ClientEventsFixture fixture) : EventImportsBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Forms 2 and 3: the calendar suite through the client against the service host.</summary>
[Collection(ClientConformanceCollection.Name)]
public sealed class ClientCalendarConformanceTests(ClientEventsFixture fixture) : EventCalendarBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Forms 2 and 3: the intake status's shape through the client against the service host.</summary>
[Collection(ClientConformanceCollection.Name)]
public sealed class ClientIntakeConformanceTests(ClientEventsFixture fixture) : EventIntakeBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}
