# WoW2.Sdk.Beta.Be.Events.Client

Forms 2 and 3 of the Events capability: every Events broker (`IEventCatalogsBroker`, `ICatalogEventsBroker`,
`IEventReviewBroker`, `IEventTrackingBroker`, `IEventWorkflowsBroker`, `IEventImportsBroker`, `IEventCalendarBroker`,
`IEventIntakeBroker`) over the running Events service's HTTP API. The product changes form by configuration: swap
`AddEvents()` for `AddEventsClient(…)` in its composition root; no other code changes, and the same conformance suites
pass on both.

```bash
dotnet add package WoW2.Sdk.Beta.Be.Events.Client --prerelease
```

```csharp
// A backend acting for the person on the current request (RFC 8693 token exchange)
services.AddEventsClient(options => options.BaseAddress = new Uri("https://events.example.com/"))
    .AddOAuth2TokenExchange(token =>
    {
        token.TokenEndpoint = new Uri("https://identity.example.com/connect/token");
        token.ClientId = "desk";
        token.ClientSecret = configuration["Events:ClientSecret"];
        token.Resource = "https://events.example.com/";            // the service root, with its trailing slash
        token.Scope = "events.read events.track";
    });

// A job acting as the service itself
services.AddEventsClient(options => options.BaseAddress = new Uri("https://events.example.com/"))
    .AddOAuth2ClientCredentials(token => { /* the same settings */ });
```

- one named `HttpClient` (`EventsClientConstants.HttpClientName`, `events`) carries every call; the client sends no
  credential of its own, so the returned builder takes the be sdk's token handler, resilience handlers or a test
  server's handler
- the wire is the contract's HTTP mirror: `{ "data": … }` answers, web JSON with camelCase string enums (`movedOnline`,
  `inviteOnly`), query names as the query records' properties in camelCase, days as `yyyy-MM-dd`
- a failure comes back by its status: 400 `Validation`, 401 `Unauthorized`, 403 `Forbidden`, 404 `NotFound`, 409
  `Conflict`; any other status, and an unreachable service, `ExternalUnavailable`; a token Identity refuses to the
  handler, `ExternalUnauthorized`
- the calendar export returns the `text/calendar` file itself as a `CalendarFile`, no envelope
- an import row's `legacy` record passes through as written
