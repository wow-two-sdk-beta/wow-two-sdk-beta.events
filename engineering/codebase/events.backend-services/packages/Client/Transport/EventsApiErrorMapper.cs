using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace WoW.Two.Sdk.Beta.Be.Events.Client.Transport;

/// <summary>Maps a failed answer back to the error the contract names, by its status; the problem body lends its message.</summary>
internal static class EventsApiErrorMapper
{
    /// <summary>Reads a failed answer's problem body, when it has one, and maps the status.</summary>
    /// <param name="response">The failed answer.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The error.</returns>
    public static async Task<AppError> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        string? message = null;
        try
        {
            if (response.Content.Headers.ContentLength is not 0)
            {
                var problem = await response.Content.ReadFromJsonAsync<EventsApiProblem>(EventsApiTransport.Json, cancellationToken);
                message = problem?.Detail ?? problem?.Title;
            }
        }
        catch (JsonException)
        {
            // A body that is no problem keeps the status's own message.
        }
        catch (NotSupportedException)
        {
            // Neither is a body of another media type.
        }

        return ToError(response.StatusCode, string.IsNullOrWhiteSpace(message) ? $"The Events service answered {(int)response.StatusCode}." : message);
    }

    /// <summary>Maps a status: 400 <c>Validation</c>, 401 <c>Unauthorized</c>, 403 <c>Forbidden</c>, 404 <c>NotFound</c>,
    /// 409 <c>Conflict</c>, any other <c>ExternalUnavailable</c>.</summary>
    /// <param name="status">The answer's status.</param>
    /// <param name="message">The error's message.</param>
    /// <returns>The error.</returns>
    public static AppError ToError(HttpStatusCode status, string message) => status switch
    {
        HttpStatusCode.BadRequest => AppErrorFactory.Validation(message),
        HttpStatusCode.Unauthorized => AppErrorFactory.Unauthorized(message),
        HttpStatusCode.Forbidden => AppErrorFactory.Forbidden(message),
        HttpStatusCode.NotFound => AppErrorFactory.NotFound(message),
        HttpStatusCode.Conflict => AppErrorFactory.Conflict(message),
        _ => AppErrorFactory.ExternalUnavailable(message),
    };
}
