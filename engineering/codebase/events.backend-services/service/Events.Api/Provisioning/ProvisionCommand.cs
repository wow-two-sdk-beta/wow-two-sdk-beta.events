using System.Text.Json;
using Events.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Catalogs;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace Events.Api.Provisioning;

/// <summary>Provisions what a deploy needs before anyone signs in, as the operator acting for the owner organization:
/// <c>catalog ensure &lt;slug&gt; --owner-org &lt;id&gt; [--owner-issuer &lt;issuer&gt;] [--name &lt;name&gt;] [--kinds a,b]</c>
/// and <c>workflow put --owner-org &lt;id&gt; [--owner-issuer &lt;issuer&gt;]</c>, which takes the workflow as
/// <c>--default &lt;key&gt; --stages key:class,…</c> (keys and classes only, the stored labels and fields kept) or as
/// <c>--spec &lt;path|-&gt;</c> (a whole <see cref="WorkflowSpec"/> as JSON, from a file or from standard input).</summary>
/// <remarks>
///   - runs the module in process over the service's database, already migrated by the deploy; no web host, no token
///   - idempotent: a rerun with the same arguments changes nothing and says so
///   - <c>--spec</c> writes the spec's stages with their labels, the default stage and the custom fields' schema, at the
///     stored revision, only when the stored workflow differs (objects compare by content, never by key order); the
///     spec's own <c>expectedRevision</c> is ignored. A spec that is not JSON, names an unknown member or fails the
///     module's own validation is refused: a <c>workflow-put-error {json}</c> line carries the full message, then
///     <c>ok=false reason=…</c>, exit 1
///   - a catalog it ensures is always private (events-integration #50); the last line carries the outcome, for runners
///     that keep only it: <c>catalog-ensure-result ok=… slug=… created=… changed=… revision=…</c>,
///     <c>workflow-put-result ok=… stages=… changed=… revision=…</c>, or <c>ok=false reason=…</c>
/// </remarks>
public static class ProvisionCommand
{
    /// <summary>Tells whether the arguments name a provisioning verb.</summary>
    /// <param name="args">The process arguments.</param>
    public static bool IsCommand(IReadOnlyList<string> args) => ProvisionArguments.IsCommand(args);

    /// <summary>Runs the verb against the host's own configuration, reading <c>--spec -</c> from standard input.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <returns>The process exit code: 0 done, 1 refused.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(args);
        var builder = WebApplication.CreateBuilder();
        builder.Configure();
        return await RunAsync(args, builder.Configuration, Console.In, output);
    }

    /// <summary>Runs the verb against a given configuration, reading <c>--spec -</c> from standard input.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="configuration">The settings: the database and the trusted issuer.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The process exit code: 0 done, 1 refused.</returns>
    public static Task<int> RunAsync(IReadOnlyList<string> args, IConfiguration configuration, TextWriter output, CancellationToken cancellationToken = default) =>
        RunAsync(args, configuration, Console.In, output, cancellationToken);

    /// <summary>Runs the verb against a given configuration and input.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="configuration">The settings: the database and the trusted issuer.</param>
    /// <param name="input">Where a <c>--spec -</c> comes from.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The process exit code: 0 done, 1 refused.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, IConfiguration configuration, TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        var prefix = args is { Count: >= 2 } && args[0] == "workflow" ? "workflow-put-result" : "catalog-ensure-result";
        if (ProvisionArguments.Parse(args) is not { } arguments)
            return await RefuseAsync(output, prefix, "usage");

        var issuer = arguments.OwnerIssuer ?? configuration["Events:Auth:Issuers:0:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
            return await RefuseAsync(output, prefix, "no-issuer");

        var connectionString = configuration["DB_CONNECTION"] is { Length: > 0 } alias ? alias : configuration["DatabaseSettings:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            return await RefuseAsync(output, prefix, "no-database");

        WorkflowSpec? spec = null;
        if (arguments.Spec is { } source)
        {
            var (read, reason, message) = await ReadSpecAsync(source, input, cancellationToken);
            if (read is null)
                return await RefuseSpecAsync(output, reason!, message!);

            spec = read;
        }

        var services = new ServiceCollection().AddLogging()
            .AddSingleton<ICurrentPrincipal>(new OperatorPrincipal(issuer.Trim(), arguments.OwnerOrganization));
        services.AddEvents(EventsModuleConstants.ServiceSchemaOwner).Configure(options => options.ConnectionString = connectionString);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var line = arguments.Verb == ProvisionArguments.CatalogEnsure
            ? await EnsureCatalogAsync(scope.ServiceProvider.GetRequiredService<IEventCatalogsBroker>(), arguments, cancellationToken)
            : spec is null
                ? await PutWorkflowAsync(scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>(), arguments, cancellationToken)
                : await PutWorkflowSpecAsync(scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>(), spec, output, cancellationToken);
        await output.WriteLineAsync(line);
        return line.Contains(" ok=true", StringComparison.Ordinal) ? 0 : 1;
    }

    private static async Task<string> EnsureCatalogAsync(IEventCatalogsBroker catalogs, ProvisionArguments arguments, CancellationToken cancellationToken)
    {
        const string Prefix = "catalog-ensure-result";
        var slug = arguments.Slug!;
        var current = await catalogs.GetAsync(slug, cancellationToken);
        var existing = current is Result<CatalogModel>.Success { Value: var found } ? found : null;
        var spec = new CatalogSpec
        {
            Name = arguments.Name ?? existing?.Name ?? slug,
            Visibility = CatalogVisibility.Private,
            Locales = existing?.Locales ?? [],
            Kinds = arguments.Kinds.Count > 0
                ? [.. arguments.Kinds.Select(kind => new CatalogKindModel { Key = kind.ToLowerInvariant() })]
                : existing?.Kinds ?? [],
            ExpectedRevision = existing?.Revision,
        };

        if (existing is not null && Same(existing, spec))
            return $"{Prefix} ok=true slug={slug} created=false changed=false revision={existing.Revision}";

        var put = await catalogs.PutAsync(slug, spec, cancellationToken);
        return put switch
        {
            Result<CatalogModel>.Success { Value: var catalog } =>
                $"{Prefix} ok=true slug={slug} created={Bool(existing is null)} changed=true revision={catalog.Revision}",
            Result<CatalogModel>.Failure { Error: var error } => $"{Prefix} ok=false slug={slug} reason={Reason(error.Message)}",
            _ => $"{Prefix} ok=false slug={slug} reason=unknown",
        };
    }

    private static async Task<string> PutWorkflowAsync(IEventWorkflowsBroker workflows, ProvisionArguments arguments, CancellationToken cancellationToken)
    {
        const string Prefix = "workflow-put-result";
        var stages = new List<WorkflowStageModel>();
        foreach (var item in arguments.Stages)
        {
            var parts = item.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !Enum.TryParse<StageClass>(parts[1], ignoreCase: true, out var stageClass))
                return $"{Prefix} ok=false reason=stage-{Reason(item)}";

            var key = parts[0].ToLowerInvariant();
            stages.Add(new WorkflowStageModel
            {
                Key = key,
                Label = new Dictionary<string, string> { ["en"] = char.ToUpperInvariant(key[0]) + key[1..] },
                Class = stageClass,
            });
        }

        var current = await workflows.GetAsync(cancellationToken);
        if (current is Result<WorkflowModel>.Failure { Error: var readError })
            return $"{Prefix} ok=false reason={Reason(readError.Message)}";

        var existing = ((Result<WorkflowModel>.Success)current).Value;
        if (existing.Revision > 0 && existing.DefaultStage == arguments.DefaultStage
            && existing.Stages.Select(stage => (stage.Key, stage.Class)).SequenceEqual(stages.Select(stage => (stage.Key, stage.Class))))
            return $"{Prefix} ok=true stages={stages.Count} changed=false revision={existing.Revision}";

        var put = await workflows.PutAsync(new WorkflowSpec
        {
            Stages = stages,
            DefaultStage = arguments.DefaultStage!,
            Fields = existing.Fields,
            ExpectedRevision = existing.Revision > 0 ? existing.Revision : null,
        }, cancellationToken);
        return put switch
        {
            Result<WorkflowModel>.Success { Value: var workflow } => $"{Prefix} ok=true stages={workflow.Stages.Count} changed=true revision={workflow.Revision}",
            Result<WorkflowModel>.Failure { Error: var error } => $"{Prefix} ok=false reason={Reason(error.Message)}",
            _ => $"{Prefix} ok=false reason=unknown",
        };
    }

    /// <summary>Reads the spec the verb was given: <c>-</c> is standard input, anything else a file.</summary>
    private static async Task<(WorkflowSpec? Spec, string? Reason, string? Message)> ReadSpecAsync(string source, TextReader input, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = source == "-" ? await input.ReadToEndAsync(cancellationToken) : await File.ReadAllTextAsync(source, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, "spec-unreadable", source == "-" ? "standard input could not be read" : "the spec file could not be read");
        }

        if (string.IsNullOrWhiteSpace(json))
            return (null, "spec-empty", "the spec is empty");

        try
        {
            return JsonSerializer.Deserialize<WorkflowSpec>(json, SpecOptions) is { } spec
                ? (spec, null, null)
                : (null, "spec-empty", "the spec is empty");
        }
        catch (JsonException exception)
        {
            return (null, "spec-not-json", exception.Message);
        }
    }

    private static async Task<string> PutWorkflowSpecAsync(IEventWorkflowsBroker workflows, WorkflowSpec spec, TextWriter output, CancellationToken cancellationToken)
    {
        const string Prefix = "workflow-put-result";
        var current = await workflows.GetAsync(cancellationToken);
        if (current is Result<WorkflowModel>.Failure { Error: var readError })
            return await FailAsync(output, Prefix, readError.Message);

        var existing = ((Result<WorkflowModel>.Success)current).Value;
        if (existing.Revision > 0 && Same(existing, spec))
            return $"{Prefix} ok=true stages={existing.Stages.Count} changed=false revision={existing.Revision}";

        var put = await workflows.PutAsync(spec with { ExpectedRevision = existing.Revision > 0 ? existing.Revision : null }, cancellationToken);
        return put switch
        {
            Result<WorkflowModel>.Success { Value: var workflow } => $"{Prefix} ok=true stages={workflow.Stages.Count} changed=true revision={workflow.Revision}",
            Result<WorkflowModel>.Failure { Error: var error } => await FailAsync(output, Prefix, error.Message),
            _ => $"{Prefix} ok=false reason=unknown",
        };
    }

    /// <summary>Prints the module's full message on its own line and answers the result line that carries its reason word.</summary>
    private static async Task<string> FailAsync(TextWriter output, string prefix, string message)
    {
        await output.WriteLineAsync("workflow-put-error " + JsonSerializer.Serialize(new { Message = message }, OperatorWire.Options));
        return $"{prefix} ok=false reason={Reason(message)}";
    }

    /// <summary>Tells whether the stored workflow already reads as the spec: the same stages in order with the same
    /// classes and labels, the same default stage and the same custom fields. The store keeps a JSON object's keys in its
    /// own order, so the fields compare by content.</summary>
    private static bool Same(WorkflowModel existing, WorkflowSpec spec) =>
        existing.DefaultStage == spec.DefaultStage
        && spec.Stages is { } wanted
        && existing.Stages.Count == wanted.Count
        && existing.Stages.Zip(wanted).All(pair => SameStage(pair.First, pair.Second))
        && SameFields(existing.Fields, spec.Fields);

    /// <summary>Tells whether a stored stage is the wanted one; a null stage, which the module refuses, is never the same.</summary>
    private static bool SameStage(WorkflowStageModel stored, WorkflowStageModel? wanted) =>
        wanted is not null && stored.Key == wanted.Key && stored.Class == wanted.Class && Labels(stored.Label).SequenceEqual(Labels(wanted.Label));

    /// <summary>Reads a stage's labels as the module stores them: trimmed, a blank locale or text dropped, in locale order.</summary>
    private static IEnumerable<(string Locale, string Text)> Labels(IReadOnlyDictionary<string, string>? label) =>
        (label ?? new Dictionary<string, string>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => (Locale: pair.Key.Trim(), Text: pair.Value.Trim()))
            .OrderBy(pair => pair.Locale, StringComparer.Ordinal);

    private static bool SameFields(JsonElement? stored, JsonElement? wanted) =>
        (stored, wanted) switch
        {
            (null or { ValueKind: JsonValueKind.Undefined or JsonValueKind.Null }, null or { ValueKind: JsonValueKind.Undefined or JsonValueKind.Null }) => true,
            ({ } left, { } right) => JsonElement.DeepEquals(left, right),
            _ => false,
        };

    private static async Task<int> RefuseSpecAsync(TextWriter output, string reason, string message)
    {
        await output.WriteLineAsync("workflow-put-error " + JsonSerializer.Serialize(new { Message = message }, OperatorWire.Options));
        return await RefuseAsync(output, "workflow-put-result", reason);
    }

    private static bool Same(CatalogModel existing, CatalogSpec spec) =>
        existing.Name == spec.Name
        && existing.Visibility == CatalogVisibility.Private
        && existing.Kinds.Select(kind => kind.Key).SequenceEqual(spec.Kinds.Select(kind => kind.Key));

    private static async Task<int> RefuseAsync(TextWriter output, string prefix, string reason)
    {
        await output.WriteLineAsync($"{prefix} ok=false reason={reason}");
        return 1;
    }

    /// <summary>Turns an error message into one token: lowercase words joined by hyphens, at most 60 characters.</summary>
    private static string Reason(string message)
    {
        var words = new string([.. message.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);
        var collapsed = string.Join('-', words.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > 60 ? collapsed[..60] : collapsed;
    }

    private static string Bool(bool value) => value ? "true" : "false";

    /// <summary>Gets the options a spec is read with: the verbs' wire, and a member the spec model does not know is an
    /// error, so a misspelled <c>lable</c> is refused instead of dropped.</summary>
    private static JsonSerializerOptions SpecOptions { get; } = new(OperatorWire.Options)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };
}
