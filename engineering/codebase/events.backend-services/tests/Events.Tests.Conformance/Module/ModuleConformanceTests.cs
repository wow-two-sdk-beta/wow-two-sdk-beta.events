using WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance;

namespace Events.Tests.Conformance.Module;

/// <summary>Form 1: the catalogs suite against the in-process module.</summary>
[Collection(ModuleConformanceCollection.Name)]
public sealed class ModuleCatalogsConformanceTests(ModuleEventsFixture fixture) : EventCatalogsBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Form 1: the catalog events suite against the in-process module.</summary>
[Collection(ModuleConformanceCollection.Name)]
public sealed class ModuleCatalogEventsConformanceTests(ModuleEventsFixture fixture) : CatalogEventsBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Form 1: the review suite against the in-process module.</summary>
[Collection(ModuleConformanceCollection.Name)]
public sealed class ModuleReviewConformanceTests(ModuleEventsFixture fixture) : EventReviewBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Form 1: the tracking suite against the in-process module.</summary>
[Collection(ModuleConformanceCollection.Name)]
public sealed class ModuleTrackingConformanceTests(ModuleEventsFixture fixture) : EventTrackingBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Form 1: the workflows suite against the in-process module.</summary>
[Collection(ModuleConformanceCollection.Name)]
public sealed class ModuleWorkflowsConformanceTests(ModuleEventsFixture fixture) : EventWorkflowsBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Form 1: the imports suite against the in-process module.</summary>
[Collection(ModuleConformanceCollection.Name)]
public sealed class ModuleImportsConformanceTests(ModuleEventsFixture fixture) : EventImportsBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Form 1: the calendar suite against the in-process module.</summary>
[Collection(ModuleConformanceCollection.Name)]
public sealed class ModuleCalendarConformanceTests(ModuleEventsFixture fixture) : EventCalendarBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}

/// <summary>Form 1: the intake status's shape against the in-process module.</summary>
[Collection(ModuleConformanceCollection.Name)]
public sealed class ModuleIntakeConformanceTests(ModuleEventsFixture fixture) : EventIntakeBrokerConformance
{
    /// <inheritdoc />
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}
