using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

/// <summary>Reads an SCFD subscription's pull cursor: <c>GET {BaseUrl}/api/subscriptions/{id}/events?cursor=&amp;limit=</c>,
/// over the source's named HTTP client (which carries its client-credentials token when one is configured).</summary>
/// <param name="clients">The HTTP client factory.</param>
internal sealed class ScfdObservationFeed(IHttpClientFactory clients) : IObservationFeed
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<ObservationFeedPage> ReadAsync(EventsIntakeSourceOptions source, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(IntakeSources.ClientName(source.Name));
        var path = $"api/subscriptions/{Uri.EscapeDataString(IntakeSources.Subscription(source))}/events?limit={limit.ToString(CultureInfo.InvariantCulture)}"
            + (string.IsNullOrEmpty(cursor) ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
        var uri = new Uri(new Uri(source.BaseUrl.TrimEnd('/') + "/"), path);

        using var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"SCFD answered {(int)response.StatusCode} for source '{source.Name}'.", null, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ScfdEnvelope<ScfdCursorPage>>(Wire, cancellationToken);
        var page = envelope?.Data ?? throw new InvalidDataException($"SCFD answered no page for source '{source.Name}'.");
        return new ObservationFeedPage(page.Data ?? [], page.Next, page.HasMore);
    }

    /// <inheritdoc />
    public async Task<ScfdSubscription> ReadSubscriptionAsync(EventsIntakeSourceOptions source, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(IntakeSources.ClientName(source.Name));
        var uri = new Uri(new Uri(source.BaseUrl.TrimEnd('/') + "/"), $"api/subscriptions/{Uri.EscapeDataString(IntakeSources.Subscription(source))}");

        using var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"SCFD answered {(int)response.StatusCode} for the subscription of source '{source.Name}'.", null, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ScfdEnvelope<JsonElement>>(Wire, cancellationToken);
        return envelope is { Data: var data } && ScfdSubscription.Read(data) is { } subscription
            ? subscription
            : throw new InvalidDataException($"SCFD answered no subscription for source '{source.Name}'.");
    }
}
