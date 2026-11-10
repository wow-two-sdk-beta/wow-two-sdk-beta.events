# WoW2.Sdk.Beta.Be.Events.Module

Form 1 of the Events capability: `IGreetingsBroker` in the consumer's process, over the consumer's PostgreSQL database.
The module owns the schema `events`: `AddEvents()` registers its embedded `Migrations/NNN-name/{Apply,Rollback}.sql` as
the named set `events`, with its own history and lock, beside the consumer's own set. The consumer's `migrate` verbs
(the be sdk's `MigrateCommand`) apply it with every other set, and a local run's boot does.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Events.Module --prerelease
```

```csharp
services.AddEvents().Configure(options => options.ConnectionString = "Host=…;Database=…");
```
