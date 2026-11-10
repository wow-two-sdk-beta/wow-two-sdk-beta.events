using System.Text.Json;
using System.Text.Json.Serialization;
using Events.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.Imports;
using WoW.Two.Sdk.Beta.Be.Events.Module;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Identity.CurrentPrincipal;

namespace Events.Api.Provisioning;

/// <summary>Imports a <c>wow2.events-import.v1</c> bundle as the deploy's operator for the owner organization:
/// <c>import &lt;catalog&gt; --owner-org &lt;id&gt; [--owner-issuer &lt;issuer&gt;] [--dry-run] [--file &lt;path&gt;]</c>, the bundle
/// read from the file or, without one, from standard input.</summary>
/// <remarks>
///   - onboards a tenant's own events into its catalog, as Wheelhouse moves a product's data: the same rules as the import
///     API (whole bundle validated first, id map stable on every rerun), with no person token
///   - prints the report as one <c>import-report {json}</c> line, then the last line, for runners that keep only it:
///     <c>import-result ok=… dry-run=… rows=… created=… updated=… unchanged=… failed=…</c>, or <c>ok=false reason=…</c>
/// </remarks>
public static class ImportCommand
{
    private static readonly JsonSerializerOptions Wire = CreateWire();

    /// <summary>Tells whether the arguments name the import verb.</summary>
    /// <param name="args">The process arguments.</param>
    public static bool IsCommand(IReadOnlyList<string> args) => args is { Count: >= 1 } && args[0] == "import";

    /// <summary>Runs the verb against the host's own configuration, reading a bundle without <c>--file</c> from standard input.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <returns>The process exit code: 0 done, 1 refused.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configure();
        return await RunAsync(args, builder.Configuration, Console.In, output);
    }

    /// <summary>Runs the verb against a given configuration and input.</summary>
    /// <param name="args">The process arguments.</param>
    /// <param name="configuration">The settings: the database and the trusted issuer.</param>
    /// <param name="input">Where the bundle comes from without <c>--file</c>.</param>
    /// <param name="output">Where the result lines go.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The process exit code: 0 done, 1 refused.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, IConfiguration configuration, TextReader input, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (!TryParse(args, out var catalog, out var org, out var issuer, out var dryRun, out var file))
            return await RefuseAsync(output, "usage");

        issuer ??= configuration["Events:Auth:Issuers:0:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
            return await RefuseAsync(output, "no-issuer");

        var connectionString = configuration["DB_CONNECTION"] is { Length: > 0 } alias ? alias : configuration["DatabaseSettings:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            return await RefuseAsync(output, "no-database");

        EventImportBundle? bundle;
        try
        {
            var json = file is null ? await input.ReadToEndAsync(cancellationToken) : await File.ReadAllTextAsync(file, cancellationToken);
            bundle = JsonSerializer.Deserialize<EventImportBundle>(json, Wire);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return await RefuseAsync(output, exception is JsonException ? "bundle-not-json" : "bundle-unreadable");
        }

        if (bundle is null)
            return await RefuseAsync(output, "bundle-empty");

        var services = new ServiceCollection().AddLogging()
            .AddSingleton<ICurrentPrincipal>(new OperatorPrincipal(issuer.Trim(), org));
        services.AddEvents(EventsModuleConstants.ServiceSchemaOwner).Configure(options => options.ConnectionString = connectionString);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IEventImportsBroker>().ImportAsync(catalog, bundle, dryRun, cancellationToken);
        if (result is Result<EventImportReport>.Failure { Error: var error })
        {
            await output.WriteLineAsync("import-error " + JsonSerializer.Serialize(new { error.Message }, Wire));
            return await RefuseAsync(output, Reason(error.Message));
        }

        var report = ((Result<EventImportReport>.Success)result).Value;
        await output.WriteLineAsync("import-report " + JsonSerializer.Serialize(report, Wire));
        await output.WriteLineAsync(
            $"import-result ok={Bool(report.Failed == 0)} dry-run={Bool(report.DryRun)} rows={report.Map.Count} created={report.Created} " +
            $"updated={report.Updated} unchanged={report.Unchanged} failed={report.Failed}");
        return report.Failed == 0 ? 0 : 1;
    }

    private static bool TryParse(IReadOnlyList<string> args, out string catalog, out string org, out string? issuer, out bool dryRun, out string? file)
    {
        catalog = org = string.Empty;
        issuer = file = null;
        dryRun = false;
        if (args.Count < 2 || args[1].StartsWith("--", StringComparison.Ordinal))
            return false;

        catalog = args[1];
        for (var index = 2; index < args.Count; index++)
        {
            if (args[index] == "--dry-run")
            {
                dryRun = true;
                continue;
            }

            if (index + 1 >= args.Count)
                return false;
            var value = args[++index];
            switch (args[index - 1])
            {
                case "--owner-org": org = value.Trim(); break;
                case "--owner-issuer": issuer = value.Trim(); break;
                case "--file": file = value == "-" ? null : value; break;
                default: return false;
            }
        }

        return org.Length > 0;
    }

    private static async Task<int> RefuseAsync(TextWriter output, string reason)
    {
        await output.WriteLineAsync($"import-result ok=false reason={reason}");
        return 1;
    }

    private static string Reason(string message)
    {
        var words = new string([.. message.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);
        var collapsed = string.Join('-', words.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > 60 ? collapsed[..60] : collapsed;
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static JsonSerializerOptions CreateWire()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
