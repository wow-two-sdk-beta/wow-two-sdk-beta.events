namespace Events.Api.Provisioning;

/// <summary>Holds a provisioning verb's arguments: <c>catalog ensure &lt;slug&gt;</c> or <c>workflow put</c>.</summary>
/// <param name="Verb">The verb: <c>catalog ensure</c> or <c>workflow put</c>.</param>
/// <param name="Slug">The catalog's slug, for <c>catalog ensure</c>.</param>
/// <param name="OwnerOrganization">The owner organization's id at its issuer.</param>
/// <param name="OwnerIssuer">The owner's issuer; none takes <c>Events:Auth:Issuers:0:Issuer</c>.</param>
/// <param name="Name">The catalog's display name.</param>
/// <param name="Kinds">The catalog's kind keys.</param>
/// <param name="Stages">The workflow's stages, <c>key:class</c> each.</param>
/// <param name="DefaultStage">The workflow's default stage.</param>
public sealed record ProvisionArguments(
    string Verb,
    string? Slug,
    string OwnerOrganization,
    string? OwnerIssuer,
    string? Name,
    IReadOnlyList<string> Kinds,
    IReadOnlyList<string> Stages,
    string? DefaultStage)
{
    /// <summary>Holds the catalog verb.</summary>
    public const string CatalogEnsure = "catalog ensure";

    /// <summary>Holds the workflow verb.</summary>
    public const string WorkflowPut = "workflow put";

    /// <summary>Tells whether the arguments name a provisioning verb.</summary>
    /// <param name="args">The process arguments.</param>
    public static bool IsCommand(IReadOnlyList<string> args) =>
        args is { Count: >= 2 } && ((args[0] == "catalog" && args[1] == "ensure") || (args[0] == "workflow" && args[1] == "put"));

    /// <summary>Parses the arguments; none when they are incomplete or name an unknown option.</summary>
    /// <param name="args">The process arguments, the verb's two words included.</param>
    public static ProvisionArguments? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!IsCommand(args))
            return null;

        var verb = $"{args[0]} {args[1]}";
        var index = 2;
        string? slug = null;
        if (verb == CatalogEnsure)
        {
            if (args.Count <= index || args[index].StartsWith("--", StringComparison.Ordinal))
                return null;
            slug = args[index++];
        }

        string? org = null, issuer = null, name = null, defaultStage = null;
        IReadOnlyList<string> kinds = [], stages = [];
        for (; index < args.Count; index++)
        {
            var value = index + 1 < args.Count ? args[index + 1] : null;
            if (value is null)
                return null;
            switch (args[index])
            {
                case "--owner-org": org = value; break;
                case "--owner-issuer": issuer = value; break;
                case "--name" when verb == CatalogEnsure: name = value; break;
                case "--kinds" when verb == CatalogEnsure: kinds = Split(value); break;
                case "--stages" when verb == WorkflowPut: stages = Split(value); break;
                case "--default" when verb == WorkflowPut: defaultStage = value; break;
                default: return null;
            }

            index++;
        }

        if (string.IsNullOrWhiteSpace(org) || (verb == WorkflowPut && (stages.Count == 0 || string.IsNullOrWhiteSpace(defaultStage))))
            return null;

        return new ProvisionArguments(verb, slug, org.Trim(), issuer?.Trim(), name?.Trim(), kinds, stages, defaultStage?.Trim());
    }

    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
