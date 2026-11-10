namespace WoW.Two.Sdk.Beta.Be.Events.Client.Transport;

/// <summary>Ends a token client's pipeline in the Events client's own, so the handlers and the primary handler set on
/// <see cref="EventsClientServiceCollectionExtensions.AddEventsClient"/>'s builder serve token calls too.</summary>
/// <param name="handlers">Builds the Events client's pipeline.</param>
internal sealed class EventsPipelineHandler(IHttpMessageHandlerFactory handlers) : HttpMessageHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // An invoker, not an HttpClient: the request was sent once already, and only HttpClient refuses a second send.
        using var invoker = new HttpMessageInvoker(handlers.CreateHandler(EventsClientConstants.HttpClientName), disposeHandler: false);
        return await invoker.SendAsync(request, cancellationToken);
    }
}
