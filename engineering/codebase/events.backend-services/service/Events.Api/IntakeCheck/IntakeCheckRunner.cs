using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2ClientCredentials;

namespace Events.Api.IntakeCheck;

/// <summary>Runs <c>intake check</c> against each source: the source's own token pulls its subscription (200), is refused
/// the tenants list (403), and finds no other tenant's subscription (404, never 403).</summary>
/// <param name="output">Where the lines go.</param>
/// <param name="primaryHandler">Creates the handler every call ends in, for a test; none uses the network.</param>
internal sealed class IntakeCheckRunner(TextWriter output, Func<HttpMessageHandler>? primaryHandler)
{
    /// <summary>Holds the name of the source's HTTP client.</summary>
    private const string ClientName = "intake-check";

    /// <summary>Holds the page size the pull asks for.</summary>
    private const int PullLimit = 100;

    /// <summary>Checks the sources and prints one line per check, then the result line.</summary>
    /// <param name="sources">The configured sources.</param>
    /// <param name="arguments">The options.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>Whether every check matched.</returns>
    public async Task<bool> RunAsync(IReadOnlyList<EventsIntakeSourceOptions> sources, IntakeCheckArguments arguments, CancellationToken cancellationToken)
    {
        var selected = arguments.Source is { } name
            ? sources.Where(source => string.Equals(source.Name, name, StringComparison.Ordinal)).ToList()
            : sources.Where(source => source.Enabled).ToList();
        if (arguments.Source is { } missing && selected.Count == 0)
        {
            await output.WriteLineAsync(IntakeCheckLine.Skipped(missing, "not-found"));
            await output.WriteLineAsync(IntakeCheckLine.Result(false));
            return false;
        }

        if (selected.Count == 0)
        {
            await output.WriteLineAsync(IntakeCheckLine.Result(false, "no-sources"));
            return false;
        }

        var ok = true;
        var summaries = new List<string>();
        foreach (var source in selected)
        {
            var (passed, summary) = await CheckAsync(source, arguments.ForeignSubscription, cancellationToken);
            ok &= passed;
            summaries.Add(summary);
        }

        // Wheelhouse Run keeps the last line only: it carries every status.
        await output.WriteLineAsync(IntakeCheckLine.Result(ok, summaries));
        return ok;
    }

    private async Task<(bool Ok, string Summary)> CheckAsync(EventsIntakeSourceOptions source, string? foreign, CancellationToken cancellationToken)
    {
        if (!IsConfigured(source, out var baseUrl, out var tokenEndpoint))
        {
            await output.WriteLineAsync(IntakeCheckLine.Skipped(source.Name, "not-configured"));
            return (false, IntakeCheckLine.Summary(source.Name, "not-configured", null, null, null, null, null, foreign is not null));
        }

        await using var services = Services(source, tokenEndpoint);
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
        var subscriptions = $"{baseUrl}/api/subscriptions";

        var pull = await SendAsync(client, $"{subscriptions}/{Uri.EscapeDataString(source.SubscriptionId.Trim())}/events?limit={PullLimit}", count: true, cancellationToken);
        await output.WriteLineAsync(IntakeCheckLine.Check(source.Name, IntakeCheckLine.Pull, pull.Status, HttpStatusCode.OK, pull.Records, pull.EventRecords, pull.Reason));
        var ok = pull.Status == HttpStatusCode.OK;

        var tenants = await SendAsync(client, $"{baseUrl}/api/tenants", count: false, cancellationToken);
        await output.WriteLineAsync(IntakeCheckLine.Check(source.Name, IntakeCheckLine.Tenants, tenants.Status, HttpStatusCode.Forbidden, reason: tenants.Reason));
        ok &= tenants.Status == HttpStatusCode.Forbidden;

        HttpStatusCode? foreignStatus = null;
        if (!string.IsNullOrWhiteSpace(foreign))
        {
            var other = await SendAsync(client, $"{subscriptions}/{Uri.EscapeDataString(foreign.Trim())}/events", count: false, cancellationToken);
            await output.WriteLineAsync(IntakeCheckLine.Check(source.Name, IntakeCheckLine.Foreign, other.Status, HttpStatusCode.NotFound, reason: other.Reason));
            ok &= other.Status == HttpStatusCode.NotFound;
            foreignStatus = other.Status;
        }

        // The token's outcome is the first answer's: any HTTP status means a token was issued and sent.
        var token = pull.Status is not null || tenants.Status is not null ? "ok" : pull.Reason ?? tenants.Reason ?? "unknown";
        return (ok, IntakeCheckLine.Summary(source.Name, token, pull.Status, pull.Records, pull.EventRecords, tenants.Status, foreignStatus, !string.IsNullOrWhiteSpace(foreign)));
    }

    /// <summary>Tells whether a source names everything a check needs: its base URL, its subscription and its token client.</summary>
    private static bool IsConfigured(EventsIntakeSourceOptions source, out string baseUrl, out Uri tokenEndpoint)
    {
        baseUrl = (source.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        tokenEndpoint = null!;
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out _)
            && !string.IsNullOrWhiteSpace(source.SubscriptionId)
            && Uri.TryCreate(source.TokenEndpoint, UriKind.Absolute, out tokenEndpoint!)
            && !string.IsNullOrWhiteSpace(source.ClientId)
            && !string.IsNullOrWhiteSpace(source.ClientSecret);
    }

    /// <summary>Builds one client for the source, with the be sdk's client-credentials handler.</summary>
    private ServiceProvider Services(EventsIntakeSourceOptions source, Uri tokenEndpoint)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        var client = services.AddHttpClient(ClientName).AddOAuth2ClientCredentials(token =>
        {
            token.TokenEndpoint = tokenEndpoint;
            token.ClientId = source.ClientId;
            token.ClientSecret = source.ClientSecret ?? string.Empty;
            token.Scope = source.Scope;
            token.Resource = source.TokenResource();
        });
        if (primaryHandler is not null)
        {
            client.ConfigurePrimaryHttpMessageHandler(primaryHandler);
            services.AddHttpClient(OAuth2HttpClientNames.TokenEndpoint).ConfigurePrimaryHttpMessageHandler(primaryHandler);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>Sends one check's request; reads the pull's records when asked. Nothing of the answer but its status and
    /// counts leaves this method.</summary>
    private static async Task<CheckAnswer> SendAsync(HttpClient client, string url, bool count, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (!count || response.StatusCode != HttpStatusCode.OK)
                return new CheckAnswer(response.StatusCode, null, null, null);

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            return Count(response.StatusCode, await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken));
        }
        catch (OAuth2TokenRequestException exception)
        {
            return new CheckAnswer(null, null, null, "token-refused:" + IntakeCheckLine.Code(exception.Error));
        }
        catch (HttpRequestException)
        {
            return new CheckAnswer(null, null, null, "unreachable");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CheckAnswer(null, null, null, "timeout");
        }
        catch (JsonException)
        {
            return new CheckAnswer(HttpStatusCode.OK, 0, 0, "malformed");
        }
    }

    /// <summary>Counts the records of an SCFD page, <c>{ data: { data: [record], next, hasMore } }</c>, and those whose
    /// <c>data.schema</c> is <c>wow2.event.v1</c>.</summary>
    private static CheckAnswer Count(HttpStatusCode status, JsonDocument document)
    {
        using (document)
        {
            if (!document.RootElement.TryGetProperty("data", out var page) || !page.TryGetProperty("data", out var records) || records.ValueKind != JsonValueKind.Array)
                return new CheckAnswer(status, 0, 0, "malformed");

            var events = records.EnumerateArray().Count(record =>
                record.ValueKind == JsonValueKind.Object
                && record.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("schema", out var schema)
                && schema.ValueKind == JsonValueKind.String
                && schema.GetString() == EventsConstants.EventSchema);
            return new CheckAnswer(status, records.GetArrayLength(), events, null);
        }
    }

    /// <summary>Represents one check's answer: its status, the pull's counts and why no answer came.</summary>
    private sealed record CheckAnswer(HttpStatusCode? Status, int? Records, int? EventRecords, string? Reason);
}
