using WoW.Two.Sdk.Beta.Be.Events.Module.Persistence;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Imports;

/// <summary>Collects the rows an import adds, so they are saved in an order the foreign keys accept: the context declares
/// no relationships, so it cannot order them itself.</summary>
internal sealed class ImportWrites
{
    /// <summary>Gets the events to create, saved first.</summary>
    public List<CatalogEventEntity> Events { get; } = [];

    /// <summary>Gets the events whose deadline rows are replaced.</summary>
    public List<Guid> ReplacedDeadlines { get; } = [];

    /// <summary>Gets the deadline rows to add.</summary>
    public List<EventDeadlineEntity> Deadlines { get; } = [];

    /// <summary>Gets the source identities to add.</summary>
    public List<EventSourceEntity> Sources { get; } = [];

    /// <summary>Gets the tracking to create.</summary>
    public List<TrackingEntity> Tracking { get; } = [];

    /// <summary>Gets the id map rows to add.</summary>
    public List<ImportMapEntity> Map { get; } = [];

    /// <summary>Gets the evidence observations to add.</summary>
    public List<ObservationEntity> Observations { get; } = [];

    /// <summary>Gets the history entries to add, saved last.</summary>
    public List<TrackingHistoryEntity> History { get; } = [];
}
