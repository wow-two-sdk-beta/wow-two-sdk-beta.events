# wow-two-sdk-beta.events — Contract

*Last updated: 2026-11-10*

> One broker per capability, the same in every form. Rules: wow-two-ws/conventions/development/capability-forms.md
> § Contract. Types: `packages/Abstractions` (namespaces under `WoW.Two.Sdk.Beta.Be.Events`).

## Brokers

| Broker | Members | Who |
|---|---|---|
| `IEventCatalogsBroker` | `GetAsync(slug)`, `ListAsync()`, `PutAsync(slug, CatalogSpec)` | reads: owner org, or anyone for a public catalog; put: creates for the caller's org, or replaces a catalog it owns |
| `ICatalogEventsBroker` | `ListAsync(catalog, EventQuery)`, `GetAsync`, `CreateAsync(EventSpec)`, `ProposeAsync(EventSpec)`, `UpdateAsync`, `ArchiveAsync(expectedRevision)` | reads: published events for readers, every state for the owner; writes: owner org; a curator's entry publishes, a member's proposal is a candidate |
| `IEventReviewBroker` | `ListAsync(ReviewQuery)`, `AcceptAsync(id, ReviewDecision)`, `RejectAsync`, `MergeAsync(id, MergeDecision)`, `ApplyChangeAsync(observationId, ReviewDecision)`, `DismissChangeAsync`, `ListObservationsAsync(ObservationQuery)`, `GetObservationAsync` | owner org |
| `IEventTrackingBroker` | `ListAsync(catalog, TrackingQuery)`, `GetAsync(catalog, eventId)`, `PutAsync(TrackingSpec)`, `DeleteAsync(expectedRevision)`, `GetHistoryAsync`, `GetSummaryAsync(today)` | the caller's active organization; its own tracking only |
| `IEventWorkflowsBroker` | `GetAsync()`, `PutAsync(WorkflowSpec)` | the caller's active organization |
| `IEventImportsBroker` | `ImportAsync(catalog, EventImportBundle, dryRun)` | owner org; tracking lands in the caller's org |
| `IEventCalendarBroker` | `ExportAsync(catalog, EventQuery)` → `CalendarFile` | as a catalog read |
| `IEventIntakeBroker` | `GetStatusAsync()`, `PollAsync()` | operators (`events.admin`) |

- Every call acts for the current principal (the kernel's `ICurrentPrincipal`); a Form 1 host supplies its own.
- Record rules live in the module, on Events' rows: a catalog the caller cannot read answers `NotFound`, never
  `Forbidden`; tracking keys by the caller's (`iss`, `org_id`), so another organization's tracking never answers.
- Failures are `Result` errors from `EventsErrors`: `NotFound` (catalog, event, observation, tracking), `Conflict`
  (revision, key, review state), `Forbidden` (organization required, not the catalog's owner), `Validation`.
- Wire enums are camelCase strings (`movedOnline`, `inviteOnly`, `earlyBird`); dates are ISO strings; a localized text
  is an object of locale → text.

## HTTP mirror

Every answer is `{ "data": … }`; a failure is a problem body with the status below. Routes are relative to the
service root; `{c}` is a catalog slug. Query names are the query records' properties in camelCase.

| Verb and route | Broker member | Policy (scope · person's permission) |
|---|---|---|
| `GET api/events/v1/catalogs` | catalogs `ListAsync` | `events.read` · `events:read` |
| `GET api/events/v1/catalogs/{c}` | catalogs `GetAsync` | `events.read` · `events:read` |
| `PUT api/events/v1/catalogs/{c}` (body `CatalogSpec`) | catalogs `PutAsync` | `events.admin` · `events:admin` |
| `GET api/events/v1/catalogs/{c}/events?from&to&kind&country&city&text&status&review&deadlineBefore&includeUndated&cursor&limit` | events `ListAsync` | `events.read` · `events:read` |
| `GET …/catalogs/{c}/events/{id}` | events `GetAsync` | `events.read` · `events:read` |
| `POST …/catalogs/{c}/events` (body `EventSpec`) | events `CreateAsync` → 201 | `events.review` · `events:review` |
| `POST …/catalogs/{c}/candidates` (body `EventSpec`) | events `ProposeAsync` → 201, a candidate | `events.track` · `events:track` |
| `PUT …/catalogs/{c}/events/{id}` (body `EventSpec`) | events `UpdateAsync` | `events.review` · `events:review` |
| `DELETE …/catalogs/{c}/events/{id}?expectedRevision=` | events `ArchiveAsync` | `events.review` · `events:review` |
| `GET …/catalogs/{c}/events.ics?<event filters>` | calendar `ExportAsync` → `text/calendar` file, no envelope | `events.read` · `events:read` |
| `GET …/catalogs/{c}/review?cursor&limit` | review `ListAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/review/{id}/accept` (body `ReviewDecision`) | review `AcceptAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/review/{id}/reject?expectedRevision=` | review `RejectAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/review/{id}/merge` (body `MergeDecision`) | review `MergeAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/observations/{id}/apply` (body `ReviewDecision`) | review `ApplyChangeAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/observations/{id}/dismiss` | review `DismissChangeAsync` | `events.review` · `events:review` |
| `GET …/catalogs/{c}/observations?state&eventId&cursor&limit` | review `ListObservationsAsync` | `events.review` · `events:review` |
| `GET …/catalogs/{c}/observations/{id}` | review `GetObservationAsync` | `events.review` · `events:review` |
| `GET …/catalogs/{c}/tracking?trackedOnly&stage&priority&from&to&kind&country&text&deadlineBefore&includeUndated&cursor&limit` | tracking `ListAsync` | `events.read` · `events:read` |
| `GET …/catalogs/{c}/tracking/summary?today=` | tracking `GetSummaryAsync` | `events.read` · `events:read` |
| `GET …/catalogs/{c}/tracking/{eventId}` | tracking `GetAsync` | `events.read` · `events:read` |
| `PUT …/catalogs/{c}/tracking/{eventId}` (body `TrackingSpec`) | tracking `PutAsync` | `events.track` · `events:track` |
| `DELETE …/catalogs/{c}/tracking/{eventId}?expectedRevision=` | tracking `DeleteAsync` | `events.track` · `events:track` |
| `GET …/catalogs/{c}/tracking/{eventId}/history` | tracking `GetHistoryAsync` | `events.read` · `events:read` |
| `POST …/catalogs/{c}/imports?dryRun=` (body `EventImportBundle`) | imports `ImportAsync` | `events.import` · `events:admin` |
| `GET api/events/v1/workflow` | workflows `GetAsync` | `events.read` · `events:read` |
| `PUT api/events/v1/workflow` (body `WorkflowSpec`) | workflows `PutAsync` | `events.track` · `events:admin` |
| `GET api/events/v1/intake` | intake `GetStatusAsync` | `events.admin` · `events:admin` |
| `POST api/events/v1/intake/poll` | intake `PollAsync` | `events.admin` · `events:admin` |
| `POST signals/identity` | Identity's revocation signals | signed by the trusted issuer |

| Error type | Status |
|---|---|
| `Validation` | 400 |
| `Unauthorized` (no token) | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |

- A person's call needs the scope and the permission; a service's call (client credentials) the scope alone.
- Browsers never call the service directly, except the console's own pages through its BFF session; products proxy
  through their backends (Desk: token exchange, `api-communication.md`).

## Forms

| Form | Implementation | Wire |
|---|---|---|
| 1 | `Module*Broker` (Module), over PostgreSQL schema `events` | in process |
| 2/3 | `Api*Broker` (Client), `AddEventsClient()` plus the be sdk's `AddOAuth2TokenExchange` or `AddOAuth2ClientCredentials` on the returned builder | the HTTP mirror above |
| fe | `createEventsClient` (`fe-events/kit`) | the same routes, under the host's proxy prefix |

- Conformance: one suite per broker in `packages/Testing`, run against the module, the client against the service
  host, and the fakes — `tests/Events.Tests.Conformance`.

## Intake (module-internal)

- `AddEventsIntake()` registers the SCFD pull: each source in `Events:Intake:Sources` names a catalog, an SCFD base
  URL, a subscription, a token client and a poll interval; the poller is a hosted service.
- One page per transaction: observations, source posts, event changes and the cursor commit together.
- SCFD wire (`GET api/subscriptions/{id}/events?cursor=&limit=`, `{ data: { data: [DeliveryEvent], next, hasMore } }`)
  is read by Events' own reader until `Harvest.Client` is on a registry (events-integration #15).

## Imports — `wow2.events-import.v1`

```json
{
  "format": "wow2.events-import.v1",
  "source": "ocharo-desk",
  "rows": [
    {
      "legacyId": "0193…",
      "facts": {
        "key": "tashkent-fashion-week-2026",
        "title": { "und": "Tashkent Fashion Week" },
        "kind": "runway",
        "organizer": { "name": "…", "website": "https://…" },
        "place": { "venue": "…", "city": "Tashkent", "country": "UZ" },
        "starts": { "value": "2026-10", "precision": "month", "certainty": "estimated" },
        "deadlines": [ { "kind": "application", "at": { "value": "2026-08-15", "precision": "day" } } ],
        "website": "https://…"
      },
      "sourceUrl": "https://…",
      "recordedAtUtc": "2026-09-01T10:00:00Z",
      "legacy": { "…": "the producer's whole row, kept as evidence" },
      "tracking": {
        "stage": "shortlisted",
        "priority": "high",
        "notes": "…",
        "fields": { "participation": "…", "cost": "…" }
      }
    }
  ]
}
```

- Desk's `showcase_events` maps: `date_precision` `day` → `day`, `announced`; `month` → `month` with the value
  `yyyy-MM`, `announced`; `estimated` → `month`, `estimated`; `unknown` → no value. `status` → `tracking.stage`
  (Ocharo's workflow, written first with `PUT workflow`), `format` → `kind`, `scope` dropped (derived).
- The answer is `EventImportReport`: counts and one map entry per row (`legacyId` → `eventId`, `trackingId`,
  outcome); a rerun returns the same ids.
