using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2TokenExchange;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Transport;

/// <summary>Sends calls to the Events service through <see cref="EventsClientConstants.HttpClientName"/> and reads its
/// answers: the <c>{ data }</c> envelope on success, the status back into an <see cref="AppError"/> on failure.</summary>
/// <remarks>An unreachable service returns <see cref="AppErrorType.ExternalUnavailable"/>; a token Identity refuses to the
/// consumer's token handler returns <see cref="AppErrorType.ExternalUnauthorized"/>.</remarks>
/// <param name="clients">Creates the named client.</param>
/// <param name="options">The client's settings: whether it takes Identity tokens itself.</param>
/// <param name="subjects">Finds the person on the current call.</param>
internal sealed class EventsApiTransport(IHttpClientFactory clients, IOptions<EventsClientOptions> options, IOAuth2SubjectTokenSource subjects)
{
    /// <summary>Gets the wire's JSON options: the be sdk preset the service writes with — web defaults, camelCase string
    /// enums, nulls left out.</summary>
    public static JsonSerializerOptions Json => JsonOptionsConstants.Default;

    /// <summary>Sends a call without a body whose answer carries a value.</summary>
    /// <typeparam name="T">The answer's type.</typeparam>
    /// <param name="method">The method.</param>
    /// <param name="route">The route, relative to the service root.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The answer, or the error.</returns>
    public async Task<Result<T>> SendAsync<T>(HttpMethod method, string route, CancellationToken cancellationToken)
        where T : notnull
    {
        using var request = new HttpRequestMessage(method, route);
        return await ReadAsync<T>(request, cancellationToken);
    }

    /// <summary>Sends a call with a JSON body whose answer carries a value.</summary>
    /// <typeparam name="TBody">The body's type.</typeparam>
    /// <typeparam name="T">The answer's type.</typeparam>
    /// <param name="method">The method.</param>
    /// <param name="route">The route, relative to the service root.</param>
    /// <param name="body">The body.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The answer, or the error.</returns>
    public async Task<Result<T>> SendAsync<TBody, T>(HttpMethod method, string route, TBody body, CancellationToken cancellationToken)
        where T : notnull
    {
        using var request = new HttpRequestMessage(method, route) { Content = JsonContent.Create(body, options: Json) };
        return await ReadAsync<T>(request, cancellationToken);
    }

    /// <summary>Picks the client a call goes through: the bare client without Identity, for the consumer's own token
    /// handler; with it, the person's exchanged token when a person is on the call, else the service's own token, always
    /// under <see cref="EventsClientCalls.AsService"/>.</summary>
    /// <remarks>The subject token is only checked for here; the be sdk's exchange handler reads it again and owns caching.</remarks>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The named client's name.</returns>
    internal async ValueTask<string> ChooseClientAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Identity is null)
            return EventsClientConstants.HttpClientName;
        if (EventsClientCalls.IsService)
            return EventsClientConstants.ServiceHttpClientName;

        return await subjects.GetAsync(cancellationToken) is null
            ? EventsClientConstants.ServiceHttpClientName
            : EventsClientConstants.PersonHttpClientName;
    }

    /// <summary>Sends a call and hands back a successful answer, which the caller disposes.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The answer, or the error.</returns>
    public async Task<Result<HttpResponseMessage>> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var response = await clients.CreateClient(await ChooseClientAsync(cancellationToken))
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (response.IsSuccessStatusCode)
                return Result<HttpResponseMessage>.Ok(response);

            using (response)
                return Result<HttpResponseMessage>.Fail(await EventsApiErrorMapper.ReadAsync(response, cancellationToken));
        }
        catch (OAuth2TokenRequestException exception)
        {
            // Its message holds the client, the status and a registered error code, never Identity's own text.
            return Result<HttpResponseMessage>.Fail(AppErrorFactory.ExternalUnauthorized(exception.Message));
        }
        catch (AppException exception)
        {
            return Result<HttpResponseMessage>.Fail(exception.Error);
        }
        catch (HttpRequestException)
        {
            return Result<HttpResponseMessage>.Fail(AppErrorFactory.ExternalUnavailable("The Events service is unreachable."));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<HttpResponseMessage>.Fail(AppErrorFactory.ExternalUnavailable("The Events service did not answer in time."));
        }
    }

    /// <summary>Sends a request and reads the envelope's value.</summary>
    private async Task<Result<T>> ReadAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
        where T : notnull
    {
        var sent = await SendRawAsync(request, cancellationToken);
        if (sent is Result<HttpResponseMessage>.Failure failure)
            return Result<T>.Fail(failure.Error);

        using var response = ((Result<HttpResponseMessage>.Success)sent).Value;
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<EventsApiEnvelope<T>>(Json, cancellationToken);
            return envelope is { Data: { } data }
                ? Result<T>.Ok(data)
                : Result<T>.Fail(AppErrorFactory.SerializationFailed("The Events service answered without data."));
        }
        catch (JsonException exception)
        {
            return Result<T>.Fail(AppErrorFactory.SerializationFailed("The Events service answered malformed JSON.", exception));
        }
    }
}
