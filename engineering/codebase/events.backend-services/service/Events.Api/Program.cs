using Events.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Meta;

// Answer a verb, or build, configure and serve the host. The operator verbs run before any host boots, over the
// configuration and the database only: `intake check`, `catalog ensure`, `workflow put`, `import` and `event show`;
// `migrate` and `config check` are the be sdk's own.
return await ServiceHost.RunAsync(
    args,
    builder => builder.Configure(),
    app => app.ConfigureAsync(),
    verbs => verbs.AddEventsVerbs());

public partial class Program;
