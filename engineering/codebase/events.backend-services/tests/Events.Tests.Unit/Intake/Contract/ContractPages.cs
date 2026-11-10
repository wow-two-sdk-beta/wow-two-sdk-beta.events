using System.Net;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Tests.Contract;

/// <summary>Reads SCFD's golden pages (<c>Intake/Contract/wow2.event.v1/</c>, copied unchanged from harvest) and serves
/// them to the real <see cref="ScfdObservationFeed"/> as SCFD's HTTP API would. Shared by the unit and integration tiers.</summary>
internal static class ContractPages
{
    /// <summary>Holds the subscription the pages were pulled from, as harvest's writer numbers it.</summary>
    public const string SubscriptionId = "00000000-0000-7000-8000-000000000001";

    /// <summary>Holds the manifest's file name.</summary>
    public const string ManifestName = "manifest.json";

    /// <summary>Holds the subscription answer's file name.</summary>
    public const string SubscriptionName = "subscription.json";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>Gets the folder that holds harvest's files and Events' <c>source.json</c> beside it.</summary>
    public static string Folder { get; } = Find();

    /// <summary>Reads a copied file's bytes.</summary>
    /// <param name="name">The file's name.</param>
    public static byte[] Bytes(string name) => File.ReadAllBytes(Path.Combine(Folder, "wow2.event.v1", name));

    /// <summary>Reads the manifest: every file with its hash and the cases its records carry, in order.</summary>
    public static ContractManifest Manifest() =>
        JsonSerializer.Deserialize<ContractManifest>(Bytes(ManifestName), Web) ?? throw new InvalidDataException("The contract manifest is empty.");

    /// <summary>Gets the page files in pull order: every manifest file but the subscription.</summary>
    public static IReadOnlyList<ContractFile> Pages() => [.. Manifest().Files.Where(file => file.Name != SubscriptionName)];

    /// <summary>Builds a source that reads the golden subscription.</summary>
    /// <param name="configure">Sets the rest of the source.</param>
    public static EventsIntakeSourceOptions Source(Action<EventsIntakeSourceOptions>? configure = null)
    {
        var source = new EventsIntakeSourceOptions
        {
            Name = "contract",
            Catalog = "contract",
            BaseUrl = "https://scfd.contract.test/",
            SubscriptionId = SubscriptionId,
        };
        configure?.Invoke(source);
        return source;
    }

    /// <summary>Builds the real feed over a handler that answers each pull with the next of the given pages, in order, and
    /// the subscription's own <c>GET</c> with <c>subscription.json</c>.</summary>
    /// <param name="pages">The page files a pull answers with, one per pull.</param>
    /// <returns>The feed, and the handler that recorded each pull's cursor.</returns>
    public static (ScfdObservationFeed Feed, ContractScfdHandler Scfd) Feed(params string[] pages)
    {
        var handler = new ContractScfdHandler(pages);
        return (new ScfdObservationFeed(new SingleHandlerClientFactory(handler)), handler);
    }

    private static string Find()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, "tests", "Events.Tests.Unit", "Intake", "Contract");
            if (Directory.Exists(Path.Combine(path, "wow2.event.v1")))
                return path;
        }

        throw new DirectoryNotFoundException("tests/Events.Tests.Unit/Intake/Contract/wow2.event.v1 was not found above the test output.");
    }

    private sealed class SingleHandlerClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

/// <summary>Represents harvest's manifest of the folder.</summary>
/// <param name="Schema">The schema the pages carry.</param>
/// <param name="Files">Every file, in pull order.</param>
internal sealed record ContractManifest(string Schema, IReadOnlyList<ContractFile> Files);

/// <summary>Represents one file of the manifest.</summary>
/// <param name="Name">The file's name.</param>
/// <param name="Sha256">The file's SHA-256, lowercase hex.</param>
/// <param name="Cases">One case name per record of the page, in order; <c>empty</c> for a page without records.</param>
internal sealed record ContractFile(string Name, string Sha256, IReadOnlyList<string> Cases);

/// <summary>Stands in for SCFD's HTTP API over the golden files: the bytes harvest's host served, unchanged.</summary>
/// <param name="pages">The page files a pull answers with, one per pull, in order.</param>
internal sealed class ContractScfdHandler(IReadOnlyList<string> pages) : HttpMessageHandler
{
    private int pulls;

    /// <summary>Gets the <c>cursor</c> each pull sent; none for a pull without one.</summary>
    public List<string?> Cursors { get; } = [];

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var subscription = $"/api/subscriptions/{ContractPages.SubscriptionId}";
        if (request.Method == HttpMethod.Get && path == subscription)
            return Task.FromResult(Json(ContractPages.Bytes(ContractPages.SubscriptionName)));
        if (request.Method != HttpMethod.Get || path != subscription + "/events" || pulls >= pages.Count)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        var cursor = request.RequestUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair[0] == "cursor")
            .Select(pair => Uri.UnescapeDataString(pair[1]))
            .FirstOrDefault();
        Cursors.Add(cursor);
        return Task.FromResult(Json(ContractPages.Bytes(pages[pulls++])));
    }

    private static HttpResponseMessage Json(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }
}
