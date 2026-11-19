using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Intake;

/// <summary>Integrates the intake's operations — its status, an on-demand poll and a catalog's freshness — in process or
/// over the service.</summary>
/// <remarks>The status and the poll are operator calls: the service gates them with <c>events.admin</c>. The freshness is a
/// member's read of one catalog (<c>events.read</c>). The background poller runs without any of them.</remarks>
public interface IEventIntakeBroker
{
    /// <summary>Gets the intake's status.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The status.</returns>
    Task<Result<IntakeStatusModel>> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Polls every enabled source now, until each answers an empty page or a page limit, then reports.</summary>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The status after the poll.</returns>
    Task<Result<IntakeStatusModel>> PollAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets how fresh a catalog's intake is: when its sources last received records, the newest observed time, and
    /// how many sources fail. Only the catalog's owner organization reads it; another organization's call, an unknown
    /// catalog and a public catalog's outside reader all answer <see cref="EventsErrors.CatalogNotFound"/>.</summary>
    /// <param name="catalog">The catalog's slug.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The freshness; no cursor values, no error text.</returns>
    Task<Result<IntakeFreshnessModel>> GetFreshnessAsync(string catalog, CancellationToken cancellationToken = default);
}
