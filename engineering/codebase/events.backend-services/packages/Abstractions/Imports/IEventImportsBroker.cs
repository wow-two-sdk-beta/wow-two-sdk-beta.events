using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Imports;

/// <summary>Integrates imports into a catalog and the caller organization's tracking, in process or over the service.</summary>
/// <remarks>Needs the catalog's owner organization. A rerun of the same bundle finds every row through the id map, keyed
/// by (catalog, organization, <see cref="EventImportRow.LegacyId"/>), and returns the same ids.</remarks>
public interface IEventImportsBroker
{
    /// <summary>Imports a bundle, or reports what it would do.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="bundle">The bundle.</param>
    /// <param name="dryRun">Whether to write nothing.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The report; <see cref="EventsErrors.ImportInvalid"/> when any row fails validation, before any write.</returns>
    Task<Result<EventImportReport>> ImportAsync(string catalog, EventImportBundle bundle, bool dryRun, CancellationToken cancellationToken = default);
}
