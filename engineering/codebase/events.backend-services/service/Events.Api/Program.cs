using Events.Api.Configurations;
using Events.Api.IntakeCheck;
using Events.Api.Provisioning;
using WoW.Two.Sdk.Beta.Be.Data;

// The intake check: configuration only, before any web host is built (the dev done-when of the SCFD pull).
if (IntakeCheckCommand.IsCommand(args))
    return await IntakeCheckCommand.RunAsync(args, Console.Out);

// Provisioning as the deploy's operator, before anyone signs in: `catalog ensure`, `workflow put` and `import`;
// `event show` reads one stored event back.
if (ProvisionCommand.IsCommand(args))
    return await ProvisionCommand.RunAsync(args, Console.Out);
if (ImportCommand.IsCommand(args))
    return await ImportCommand.RunAsync(args, Console.Out);
if (EventShowCommand.IsCommand(args))
    return await EventShowCommand.RunAsync(args, Console.Out);

// Build and configure the host.
var builder = WebApplication.CreateBuilder(args);
builder.Configure();

// Build and configure the app.
var app = builder.Build();
if (MigrateCommand.IsCommand(args))
    return await app.RunMigrateCommandAsync(args);
await app.ConfigureAsync();

// Run.
await app.RunAsync();
return 0;

public partial class Program;
