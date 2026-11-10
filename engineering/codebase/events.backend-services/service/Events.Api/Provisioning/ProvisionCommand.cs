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
/// and <c>workflow put --owner-org &lt;id&gt; [--owner-issuer &lt;issuer&gt;] --default &lt;key&gt; --stages key:class,…</c>.</summary>
/// <remarks>
///   - runs the module in process over the service's database, already migrated by the deploy; no web host, no token
///   - idempotent: a rerun with the same arguments changes nothing and says so
///   - a catalog it ensures is always private (events-integration #50); the last line carries the outcome, for runners
///     that keep only it: <c>catalog-ensure-result ok=… slug=… created=… changed=… revision=…</c>,
///     <c>workflow-put-result ok=… stages=… changed=… revision=…</c>, or <c>ok=false reason=…</c>
/// </remarks>
public static class ProvisionCommand
{
    /// <summary>Tells whether the arguments name a provisioning verb.</summary>
    /// <param name="args">The process arguments.</param>
    public static bool IsCommand(IReadOnlyList<string> args) => ProvisionArguments.IsCommand(args);

    /// <summary>Runs the verb against the host's own configuration.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <returns>The process exit code: 0 done, 1 refused.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(args);
        var builder = WebApplication.CreateBuilder();
        builder.Configure();
        return await RunAsync(args, builder.Configuration, output);
    }

    /// <summary>Runs the verb against a given configuration.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="configuration">The settings: the database and the trusted issuer.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The process exit code: 0 done, 1 refused.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, IConfiguration configuration, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
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

        var services = new ServiceCollection().AddLogging()
            .AddSingleton<ICurrentPrincipal>(new OperatorPrincipal(issuer.Trim(), arguments.OwnerOrganization));
        services.AddEvents(EventsModuleConstants.ServiceSchemaOwner).Configure(options => options.ConnectionString = connectionString);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var line = arguments.Verb == ProvisionArguments.CatalogEnsure
            ? await EnsureCatalogAsync(scope.ServiceProvider.GetRequiredService<IEventCatalogsBroker>(), arguments, cancellationToken)
            : await PutWorkflowAsync(scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>(), arguments, cancellationToken);
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
}
