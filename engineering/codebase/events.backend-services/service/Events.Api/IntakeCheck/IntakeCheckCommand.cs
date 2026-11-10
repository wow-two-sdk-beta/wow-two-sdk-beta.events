using Events.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Events.Module.Intake;

namespace Events.Api.IntakeCheck;

/// <summary>Answers <c>dotnet Events.Api.dll intake check [--source &lt;name&gt;] [--foreign-subscription &lt;id&gt;]</c>, the
/// dev done-when of the SCFD pull: per source, its own client-credentials token pulls its subscription (200), is refused
/// the tenants list (403), and finds no other tenant's subscription (404). Configuration only: no database, no Kestrel.</summary>
/// <remarks>Prints one <c>intake-check</c> line per check and a last <c>intake-check-result ok=…</c> line; exits 0 when
/// every check matches, else 1. It never prints a token, a header, a body, a base URL or a host name.</remarks>
public static class IntakeCheckCommand
{
    /// <summary>Holds the command's first word.</summary>
    public const string Name = "intake";

    /// <summary>Holds the command's verb.</summary>
    public const string Verb = "check";

    /// <summary>Tells whether the arguments name this command.</summary>
    /// <param name="args">The process's arguments.</param>
    /// <returns>Whether they start with <c>intake check</c>.</returns>
    public static bool IsCommand(IReadOnlyList<string> args) =>
        args is { Count: >= 2 } && args[0] == Name && args[1] == Verb;

    /// <summary>Runs the command over the host's own configuration: the settings files a target mounts and the environment.</summary>
    /// <param name="args">The process's arguments, <c>intake check</c> first.</param>
    /// <param name="output">Where the lines go.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(args);
        var builder = WebApplication.CreateBuilder();
        builder.Configure();
        return await RunAsync([.. args.Skip(2)], builder.Configuration, output);
    }

    /// <summary>Runs the command over the given configuration.</summary>
    /// <param name="args">The arguments after <c>intake check</c>.</param>
    /// <param name="configuration">The configuration holding <c>Events:Intake:Sources</c>.</param>
    /// <param name="output">Where the lines go.</param>
    /// <param name="primaryHandler">Creates the handler every call ends in, the token request included, for a test; none
    /// uses the network.</param>
    /// <param name="cancellationToken">The token that cancels the run.</param>
    /// <returns>The exit code: 0 when every check matches, else 1.</returns>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        IConfiguration configuration,
        TextWriter output,
        Func<HttpMessageHandler>? primaryHandler = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(output);
        if (IntakeCheckArguments.Parse(args) is not { } arguments)
        {
            await output.WriteLineAsync(IntakeCheckLine.Result(false, "usage"));
            return 1;
        }

        var options = configuration.GetSection(EventsIntakeOptions.SectionName).Get<EventsIntakeOptions>() ?? new EventsIntakeOptions();
        var ok = await new IntakeCheckRunner(output, primaryHandler).RunAsync([.. options.Sources.Where(source => source is not null)], arguments, cancellationToken);
        return ok ? 0 : 1;
    }
}
