namespace Events.Tests.Conformance.Module;

/// <summary>Shares one <see cref="ModuleEventsFixture"/> (one container) across every module suite.</summary>
[CollectionDefinition(Name)]
public sealed class ModuleConformanceCollection : ICollectionFixture<ModuleEventsFixture>
{
    /// <summary>The collection name every module suite joins.</summary>
    public const string Name = "events-module-conformance";
}
