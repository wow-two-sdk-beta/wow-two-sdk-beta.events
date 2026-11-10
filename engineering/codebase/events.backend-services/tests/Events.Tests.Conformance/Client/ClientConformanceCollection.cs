namespace Events.Tests.Conformance.Client;

/// <summary>Shares one <see cref="ClientEventsFixture"/> (one host, one container) across every client suite.</summary>
[CollectionDefinition(Name)]
public sealed class ClientConformanceCollection : ICollectionFixture<ClientEventsFixture>
{
    /// <summary>The collection name every client suite joins.</summary>
    public const string Name = "events-client-conformance";
}
