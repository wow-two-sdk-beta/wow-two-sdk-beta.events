using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;
using WoW.Two.Sdk.Beta.Be.Events.Module.Shared;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Holds what every record of one intake page shares.</summary>
/// <param name="Source">The source.</param>
/// <param name="Subscription">The subscription as stored.</param>
/// <param name="Catalog">The catalog it delivers into.</param>
/// <param name="Now">The page's time.</param>
internal sealed record IntakePageState(EventsIntakeSourceOptions Source, string Subscription, CatalogEntity Catalog, DateTimeOffset Now)
{
    /// <summary>Gets the catalog's kind keys.</summary>
    public IReadOnlyCollection<string> Kinds { get; } = CatalogAccess.KindKeys(Catalog);
}
