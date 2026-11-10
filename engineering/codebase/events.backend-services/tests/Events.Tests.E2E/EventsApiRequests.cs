using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Identity.Testing;
using WoW.Two.Sdk.Beta.Be.Testing;

namespace Events.Tests.E2E;

/// <summary>Builds the E2E tier's requests: callers with tokens, JSON bodies, and the envelope's data.</summary>
internal static class EventsApiRequests
{
    /// <summary>Holds the catalogs route.</summary>
    public const string Catalogs = "/" + EventsRouteConstants.Catalogs;

    /// <summary>Returns a person of a fresh organization holding the given permissions.</summary>
    public static FakePerson Person(params string[] permissions) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Role = "member",
        Permissions = permissions,
    };

    /// <summary>Returns a client of <paramref name="host"/> that sends <paramref name="token"/>.</summary>
    public static HttpClient Bearer(WebApiTestHost<Program> host, string token)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Returns a JSON body.</summary>
    public static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>Reads the envelope's <c>data</c>.</summary>
    public static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("data").Clone();
    }
}
