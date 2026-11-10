using WoW.Two.Sdk.Beta.Be.Events.Client.Transport;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Intake;

/// <summary>Integrates the running Events service's intake status, an operator's view (<c>events.admin</c>).</summary>
/// <param name="transport">The service's wire.</param>
internal sealed class ApiEventIntakeBroker(EventsApiTransport transport) : IEventIntakeBroker
{
    /// <summary>Holds the poll's segment under the intake route.</summary>
    private const string Poll = "poll";

    /// <inheritdoc />
    public Task<Result<IntakeStatusModel>> GetStatusAsync(CancellationToken cancellationToken = default) =>
        transport.SendAsync<IntakeStatusModel>(HttpMethod.Get, EventsRouteConstants.Intake, cancellationToken);

    /// <inheritdoc />
    public Task<Result<IntakeStatusModel>> PollAsync(CancellationToken cancellationToken = default) =>
        transport.SendAsync<IntakeStatusModel>(HttpMethod.Post, $"{EventsRouteConstants.Intake}/{Poll}", cancellationToken);
}
