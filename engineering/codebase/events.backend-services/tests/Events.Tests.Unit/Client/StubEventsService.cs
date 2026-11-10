using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Events.Client;

namespace Events.Tests.Unit.Client;

/// <summary>Stands in for the Events service: answers every call with one canned response and keeps the requests it saw.</summary>
internal sealed class StubEventsService : IDisposable
{
    private readonly ServiceProvider provider;
    private Func<HttpRequestMessage, HttpResponseMessage> answer = _ => Json(HttpStatusCode.OK, """{"data":{}}""");

    public StubEventsService()
    {
        var services = new ServiceCollection();
        services.AddEventsClient(options => options.BaseAddress = new Uri("https://events.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new Handler(this));
        provider = services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Gets the requests sent, with their bodies read.</summary>
    public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = [];

    /// <summary>Resolves a broker of the client.</summary>
    public T Broker<T>() where T : notnull => provider.GetRequiredService<T>();

    /// <summary>Answers every call with <paramref name="response"/>.</summary>
    public void Answer(Func<HttpRequestMessage, HttpResponseMessage> response) => answer = response;

    /// <summary>Answers every call with a JSON body.</summary>
    public void Answer(HttpStatusCode status, string json) => answer = _ => Json(status, json);

    /// <summary>Builds a JSON response.</summary>
    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public void Dispose() => provider.Dispose();

    private sealed class Handler(StubEventsService service) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            service.Requests.Add((request.Method, request.RequestUri!.ToString(), body));
            return service.answer(request);
        }
    }
}
