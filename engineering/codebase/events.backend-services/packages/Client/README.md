# WoW2.Sdk.Beta.Be.Events.Client

Forms 2 and 3 of the Events capability: `IGreetingsBroker` over the running Events service's HTTP API. The product
changes form by configuration: swap `AddEvents()` for `AddEventsClient(…)` in its composition root; no other code
changes.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Events.Client --prerelease
```

```csharp
services.AddEventsClient(options => options.BaseAddress = new Uri("https://events.example.com/"));
```
