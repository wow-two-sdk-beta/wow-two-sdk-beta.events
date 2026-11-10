namespace Events.Api.IntakeCheck;

/// <summary>Holds the options of <c>intake check</c>.</summary>
/// <param name="Source">The one source to check; every enabled source when none.</param>
/// <param name="ForeignSubscription">Another tenant's subscription the source's token must not reach; no foreign check when none.</param>
public sealed record IntakeCheckArguments(string? Source, string? ForeignSubscription)
{
    /// <summary>Reads <c>[--source &lt;name&gt;] [--foreign-subscription &lt;id&gt;]</c>.</summary>
    /// <param name="args">The arguments after <c>intake check</c>.</param>
    /// <returns>The options; none when an argument is unknown or lacks its value.</returns>
    public static IntakeCheckArguments? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? source = null;
        string? foreign = null;
        for (var index = 0; index < args.Count; index++)
        {
            var value = index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal) ? args[index + 1] : null;
            switch (args[index])
            {
                case "--source" when value is not null:
                    source = value;
                    break;
                case "--foreign-subscription" when value is not null:
                    foreign = value;
                    break;
                default:
                    return null;
            }

            index++;
        }

        return new IntakeCheckArguments(source, foreign);
    }
}
