# WoW2.Sdk.Beta.Be.Events.Module

Form 1 of the Events capability: every Events broker in the consumer's process, over the consumer's PostgreSQL database.
The module owns the schema `events`: `AddEvents()` registers its embedded `Migrations/NNN-name/{Apply,Rollback}.sql` as
the named set `events`, with its own history and lock, beside the consumer's own set. The consumer's `migrate` verbs
(the be sdk's `MigrateCommand`) apply it with every other set, and a local run's boot does.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Events.Module --prerelease
```

```csharp
services.AddEvents().Configure(options => options.ConnectionString = "Host=…;Database=…");
```

Scheduled digests: `AddEvents()` registers the digests broker and the `log` channel; `AddEventDigests()` adds the hosted
scheduler (`Events:Digests`); `AddIdentityTelegramDigestDelivery()` adds the Telegram channel over Identity's
`IAppNoticeBroker`, which the host registers (in process, or `AddIdentityAppNotices(instance).AddOAuth2ClientCredentials(…)`
from Identity's client package with the `identity.notices.send` scope).

```csharp
services.AddEventDigests(configuration);
services.AddIdentityAppNotices(identity).AddOAuth2ClientCredentials(token => { /* Events' client, the notice audience */ });
services.AddIdentityTelegramDigestDelivery();
```
