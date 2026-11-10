using System.Net.Http.Headers;

namespace Events.Tests.Conformance.Client;

/// <summary>Sends a fresh token on every call, as the be sdk's token handlers would for a consumer.</summary>
/// <param name="token">Mints the token.</param>
internal sealed class BearerTokenHandler(Func<string> token) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token());
        return base.SendAsync(request, cancellationToken);
    }
}
