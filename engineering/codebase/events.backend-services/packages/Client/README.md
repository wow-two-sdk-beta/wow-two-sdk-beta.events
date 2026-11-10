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
// One call: the person's token exchanged for Events' audience when a person is on the call (RFC 8693; under the
// Identity BFF, the signed-in session's token), else the service's own client-credentials token
services.AddEventsClient(options =>
{
    options.BaseAddress = new Uri("https://events.example.com/");
    options.Identity = new()
    {
        TokenEndpoint = new Uri("https://identity.example.com/connect/token"),
        ClientId = "desk",
        ClientSecret = configuration["Identity:Client:ClientSecret"]!,
        // Resource defaults to the service root with its trailing slash; Scope to what Identity grants the client
    };
});

// Work a person started that runs later acts as the service, never with their token
using (EventsClientCalls.AsService())
    await catalogs.GetAsync("ocharo-fashion", cancellationToken);
```

- one named `HttpClient` (`EventsClientConstants.HttpClientName`, `events`) carries every call; with `Identity`, the
  token clients `events-person` and `events-service` end in its pipeline, so its handlers serve every call
- without `Identity` the client sends no credential of its own: chain the be sdk's `AddOAuth2TokenExchange` or
  `AddOAuth2ClientCredentials` on the returned builder, as before
- the wire is the contract's HTTP mirror: `{ "data": … }` answers, web JSON with camelCase string enums (`movedOnline`,
  `inviteOnly`), query names as the query records' properties in camelCase, days as `yyyy-MM-dd`
- a failure comes back by its status: 400 `Validation`, 401 `Unauthorized`, 403 `Forbidden`, 404 `NotFound`, 409
  `Conflict`; any other status, and an unreachable service, `ExternalUnavailable`; a token Identity refuses to the
  handler, `ExternalUnauthorized`
- the calendar export returns the `text/calendar` file itself as a `CalendarFile`, no envelope
- an import row's `legacy` record passes through as written
