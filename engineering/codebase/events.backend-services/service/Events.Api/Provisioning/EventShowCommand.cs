using System.Text.Json;
using Events.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace Events.Api.Provisioning;

/// <summary>Reads one stored event back as the deploy's operator for the owner organization:
/// <c>event show &lt;catalog&gt; --owner-org &lt;id&gt; [--owner-issuer &lt;issuer&gt;] (--id &lt;event&gt; | --key &lt;key&gt; |
/// --post &lt;scfd post&gt;)</c>, in any review state.</summary>
/// <remarks>
///   - lets an operator confirm what intake, an import or a review stored, with no person token: the event's facts, its
///     review and its sources, read through the catalog's own broker, so only the owner organization reads them
///   - <c>--key</c> matches the facts' key; <c>--post</c> an SCFD post among the event's sources
///   - prints the event as one <c>event {json}</c> line, then the last line, for runners that keep only it:
///     <c>event-show-result ok=true id=… review=… revision=… reviewed-by=… facts={json}</c>, or <c>ok=false reason=…</c>
/// </remarks>
public static class EventShowCommand
{
    /// <summary>Tells whether the arguments name the verb.</summary>
    /// <param name="args">The process arguments.</param>
    public static bool IsCommand(IReadOnlyList<string> args) => args is { Count: >= 2 } && args[0] == "event" && args[1] == "show";

    /// <summary>Runs the verb against the host's own configuration.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <returns>The process exit code: 0 found, 1 refused or not found.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configure();
        return await RunAsync(args, builder.Configuration, output);
    }

    /// <summary>Runs the verb against a given configuration.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="configuration">The settings: the database and the trusted issuer.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The process exit code: 0 found, 1 refused or not found.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, IConfiguration configuration, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(output);
        if (Parse(args) is not { } show)
            return await RefuseAsync(output, "usage");

        var issuer = show.OwnerIssuer ?? configuration["Events:Auth:Issuers:0:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
            return await RefuseAsync(output, "no-issuer");

        var connectionString = OperatorWire.ConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
            return await RefuseAsync(output, "no-database");

        var services = new ServiceCollection().AddLogging()
            .AddSingleton<ICurrentPrincipal>(new OperatorPrincipal(issuer.Trim(), show.OwnerOrganization));
        services.AddEvents(EventsModuleConstants.ServiceSchemaOwner).Configure(options => options.ConnectionString = connectionString);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var found = await FindAsync(scope.ServiceProvider.GetRequiredService<ICatalogEventsBroker>(), show, cancellationToken);
        if (found is Result<EventModel>.Failure { Error: var error })
            return await RefuseAsync(output, OperatorWire.Reason(error.Message));

        var item = ((Result<EventModel>.Success)found).Value;
        await output.WriteLineAsync("event " + JsonSerializer.Serialize(item, OperatorWire.Options));
        await output.WriteLineAsync(
            $"event-show-result ok=true id={item.Id:D} review={JsonNamingPolicy.CamelCase.ConvertName(item.Review.ToString())} " +
            $"revision={item.Revision} reviewed-by={item.ReviewedBy ?? "none"} facts={JsonSerializer.Serialize(item.Facts, OperatorWire.Options)}");
        return 0;
    }

    /// <summary>Finds the event: by id directly, by key or SCFD post through every review state's pages.</summary>
    private static async Task<Result<EventModel>> FindAsync(ICatalogEventsBroker events, ShowArguments show, CancellationToken cancellationToken)
    {
        if (show.Id is { } id)
            return await events.GetAsync(show.Catalog, id, cancellationToken);

        var postSuffix = show.Post is { } post ? $"/{post:D}" : null;
        foreach (var review in Enum.GetValues<EventReviewState>())
        {
            string? cursor = null;
            do
            {
                var query = new EventQuery { Review = review, Cursor = cursor, Limit = EventsConstants.MaxPageSize };
                var listed = await events.ListAsync(show.Catalog, query, cancellationToken);
                if (listed is Result<EventPage>.Failure { Error: var error })
                    return Result<EventModel>.Fail(error);

                var page = ((Result<EventPage>.Success)listed).Value;
                var match = page.Items.FirstOrDefault(item => postSuffix is null
                    ? string.Equals(item.Facts.Key, show.Key, StringComparison.Ordinal)
                    : item.Sources.Any(source => source.Kind == EventSourceKind.Scfd && source.Id.EndsWith(postSuffix, StringComparison.OrdinalIgnoreCase)));
                if (match is not null)
                    return Result<EventModel>.Ok(match);
                cursor = page.NextCursor;
            }
            while (cursor is not null);
        }

        return Result<EventModel>.Fail(EventsErrors.EventNotFound());
    }

    /// <summary>Parses the arguments; none when they are incomplete, name an unknown option, or name no single lookup.</summary>
    internal static ShowArguments? Parse(IReadOnlyList<string> args)
    {
        if (!IsCommand(args) || args.Count < 3 || args[2].StartsWith("--", StringComparison.Ordinal))
            return null;

        string? org = null, issuer = null, key = null;
        Guid? id = null, post = null;
        for (var index = 3; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count)
                return null;
            var value = args[index + 1].Trim();
            switch (args[index])
            {
                case "--owner-org": org = value; break;
                case "--owner-issuer": issuer = value; break;
                case "--key": key = value; break;
                case "--id" when Guid.TryParse(value, out var parsed): id = parsed; break;
                case "--post" when Guid.TryParse(value, out var parsed): post = parsed; break;
                default: return null;
            }
        }

        var lookups = (id is null ? 0 : 1) + (key is null ? 0 : 1) + (post is null ? 0 : 1);
        return string.IsNullOrEmpty(org) || lookups != 1 ? null : new ShowArguments(args[2], org, issuer, id, key, post);
    }

    private static async Task<int> RefuseAsync(TextWriter output, string reason)
    {
        await output.WriteLineAsync($"event-show-result ok=false reason={reason}");
        return 1;
    }

    /// <summary>Holds the verb's arguments: the catalog, its owner, and exactly one lookup.</summary>
    internal sealed record ShowArguments(string Catalog, string OwnerOrganization, string? OwnerIssuer, Guid? Id, string? Key, Guid? Post);
}
