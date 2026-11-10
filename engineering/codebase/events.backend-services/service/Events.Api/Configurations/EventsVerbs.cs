using Events.Api.IntakeCheck;
using Events.Api.Provisioning;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace Events.Api.Configurations;

/// <summary>Registers Events' own one-off verbs with the be sdk's <see cref="ServiceHost"/>, beside its <c>migrate</c> and
/// <c>config check</c>.</summary>
/// <remarks>Each runs before any host is built, reading the configuration itself: <c>intake check</c> calls SCFD only,
/// the operator verbs open the module over the database, and none starts a listener or a hosted service.</remarks>
public static class EventsVerbs
{
    /// <summary>Adds <c>intake check</c>, <c>catalog ensure</c>, <c>workflow put</c>, <c>import</c> and <c>event show</c>.</summary>
    /// <param name="verbs">The service's verbs.</param>
    /// <returns>The verbs, for chaining.</returns>
    public static ServiceVerbs AddEventsVerbs(this ServiceVerbs verbs)
    {
        ArgumentNullException.ThrowIfNull(verbs);
        return verbs
            .AddPreHostVerb("intake", (verb, _) => IntakeCheckCommand.RunAsync(Whole(verb), verb.Output))
            .AddPreHostVerb("catalog", (verb, _) => ProvisionCommand.RunAsync(Whole(verb), verb.Output))
            .AddPreHostVerb("workflow", (verb, _) => ProvisionCommand.RunAsync(Whole(verb), verb.Output))
            .AddPreHostVerb("import", (verb, _) => ImportCommand.RunAsync(Whole(verb), verb.Output))
            .AddPreHostVerb("event", (verb, _) => EventShowCommand.RunAsync(Whole(verb), verb.Output));
    }

    /// <summary>Puts the verb's name back in front of its arguments, as each command parses them.</summary>
    private static string[] Whole(ServiceVerbInvocation verb) => [verb.Name, .. verb.Arguments];
}
