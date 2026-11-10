using Events.Api.Configurations;
using Events.Api.IntakeCheck;
using WoW.Two.Sdk.Beta.Be.Data;

// The intake check: configuration only, before any web host is built (the dev done-when of the SCFD pull).
if (IntakeCheckCommand.IsCommand(args))
    return await IntakeCheckCommand.RunAsync(args, Console.Out);

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
