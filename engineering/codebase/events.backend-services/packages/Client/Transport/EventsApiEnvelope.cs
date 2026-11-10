namespace WoW.Two.Sdk.Beta.Be.Events.Client.Transport;

/// <summary>Represents the service's success envelope, <c>{ "data": … }</c>.</summary>
/// <typeparam name="T">The answer's type.</typeparam>
/// <param name="Data">The answer.</param>
internal sealed record EventsApiEnvelope<T>(T? Data);
