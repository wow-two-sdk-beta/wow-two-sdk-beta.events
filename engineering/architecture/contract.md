# wow-two-sdk-beta.events — Contract

*Last updated: 2026-11-17*

> One broker per capability, the same in every form. Rules: wow-two-ws/conventions/development/capability-forms.md
> § Contract. Types: `packages/Abstractions` (namespaces under `WoW.Two.Sdk.Beta.Be.Events`).

## Brokers

| Broker | Members | Who |
|---|---|---|
| `IEventCatalogsBroker` | `GetAsync(slug)`, `ListAsync()`, `PutAsync(slug, CatalogSpec)` | reads: owner org, or anyone for a public catalog; put: creates for the caller's org, or replaces a catalog it owns |
| `ICatalogEventsBroker` | `ListAsync(catalog, EventQuery)`, `GetAsync`, `CreateAsync(EventSpec)`, `ProposeAsync(EventSpec)`, `UpdateAsync`, `ArchiveAsync(expectedRevision)` | reads: published events for readers, every state for the owner; writes: owner org; a curator's entry publishes, a member's proposal publishes in a private catalog and is a candidate in a shared or public one |
| `IEventReviewBroker` | `ListAsync(ReviewQuery)`, `AcceptAsync(id, ReviewDecision)`, `RejectAsync`, `MergeAsync(id, MergeDecision)`, `SplitSourceAsync(id, SourceSplitDecision)` (one source leaves an event for a new candidate; the pair is kept apart), `ApplyChangeAsync(observationId, ReviewDecision)`, `DismissChangeAsync`, `ListObservationsAsync(ObservationQuery)`, `GetObservationAsync` | owner org |
| `IEventTrackingBroker` | `ListAsync(catalog, TrackingQuery)`, `GetAsync(catalog, eventId)`, `PutAsync(TrackingSpec)`, `DeleteAsync(expectedRevision)`, `GetHistoryAsync`, `GetSummaryAsync(today)` and `GetSummaryAsync(TrackingSummaryQuery)` (a default interface method, so an older implementation ignores the kinds) | the caller's active organization; its own tracking only |
| `IEventWorkflowsBroker` | `GetAsync()`, `PutAsync(WorkflowSpec)` | the caller's active organization |
| `IEventImportsBroker` | `ImportAsync(catalog, EventImportBundle, dryRun)` | owner org; tracking lands in the caller's org |
| `IEventCalendarBroker` | `ExportAsync(catalog, EventQuery)` → `CalendarFile` | as a catalog read |
| `IEventIntakeBroker` | `GetStatusAsync()`, `PollAsync()` | operators (`events.admin`) |
| `IEventDigestsBroker` | `ListAsync(catalog)`, `GetAsync`, `CreateAsync(DigestSpec)`, `UpdateAsync`, `DeleteAsync(expectedRevision)`, `PreviewAsync`, `SendNowAsync`, `ListRunsAsync(DigestRunQuery)` | the caller's active organization, which reads the catalog; its own digests only (`events.admin`) |

- Every call acts for the current principal (the kernel's `ICurrentPrincipal`); a Form 1 host supplies its own.
- Record rules live in the module, on Events' rows: a catalog the caller cannot read answers `NotFound`, never
  `Forbidden`; tracking keys by the caller's (`iss`, `org_id`), so another organization's tracking never answers.
- Failures are `Result` errors from `EventsErrors`: `NotFound` (catalog, event, observation, tracking, digest), `Conflict`
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
| `POST …/catalogs/{c}/candidates` (body `EventSpec`) | events `ProposeAsync` → 201, published in a private catalog, else a candidate | `events.track` · `events:track` |
| `PUT …/catalogs/{c}/events/{id}` (body `EventSpec`) | events `UpdateAsync` | `events.review` · `events:review` |
| `DELETE …/catalogs/{c}/events/{id}?expectedRevision=` | events `ArchiveAsync` | `events.review` · `events:review` |
| `GET …/catalogs/{c}/events.ics?<event filters>` | calendar `ExportAsync` → `text/calendar` file, no envelope | `events.read` · `events:read` |
| `GET …/catalogs/{c}/review?cursor&limit` | review `ListAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/review/{id}/accept` (body `ReviewDecision`) | review `AcceptAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/review/{id}/reject?expectedRevision=` | review `RejectAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/review/{id}/merge` (body `MergeDecision`) | review `MergeAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/review/{id}/split` (body `SourceSplitDecision`: `kind`, `sourceId`, `expectedRevision`; answers the new candidate) | review `SplitSourceAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/observations/{id}/apply` (body `ReviewDecision`) | review `ApplyChangeAsync` | `events.review` · `events:review` |
| `POST …/catalogs/{c}/observations/{id}/dismiss` | review `DismissChangeAsync` | `events.review` · `events:review` |
| `GET …/catalogs/{c}/observations?state&eventId&cursor&limit` | review `ListObservationsAsync` | `events.review` · `events:review` |
| `GET …/catalogs/{c}/observations/{id}` | review `GetObservationAsync` | `events.review` · `events:review` |
| `GET …/catalogs/{c}/tracking?trackedOnly&stage&stages&excludeStages&priority&from&to&kind&country&countries&excludeCountries&includeUnknownCountry&text&deadlineBefore&includeUndated&cursor&limit` | tracking `ListAsync`; list keys repeat (`countries=UZ&countries=KZ`); an untracked event reads as the workflow's default stage for `stage`, `stages` and `excludeStages` (a stage in both is a 400); an event with no country matches a country filter only with `includeUnknownCountry=true`; a two-letter `text` also finds the events in that country | `events.read` · `events:read` |
| `GET …/catalogs/{c}/tracking/summary?today=&deadlineKinds=` | tracking `GetSummaryAsync(TrackingSummaryQuery)`: `byStage` (tracked events), `untracked` (published events the organization does not track), `upcomingDeadlines` (the nearest five of published events not cancelled, untracked ones under the default stage with `tracked: false`, lost stages left out, only the repeated `deadlineKinds` when given) | `events.read` · `events:read` |
| `GET …/catalogs/{c}/tracking/{eventId}` | tracking `GetAsync` | `events.read` · `events:read` |
| `PUT …/catalogs/{c}/tracking/{eventId}` (body `TrackingSpec`) | tracking `PutAsync` | `events.track` · `events:track` |
| `DELETE …/catalogs/{c}/tracking/{eventId}?expectedRevision=` | tracking `DeleteAsync` | `events.track` · `events:track` |
| `GET …/catalogs/{c}/tracking/{eventId}/history` | tracking `GetHistoryAsync` | `events.read` · `events:read` |
| `POST …/catalogs/{c}/imports?dryRun=` (body `EventImportBundle`) | imports `ImportAsync` | `events.import` · `events:admin` |
| `GET api/events/v1/workflow` | workflows `GetAsync` | `events.read` · `events:read` |
| `PUT api/events/v1/workflow` (body `WorkflowSpec`) | workflows `PutAsync` | `events.track` · `events:admin` |
| `GET api/events/v1/intake` | intake `GetStatusAsync` | `events.admin` · `events:admin` |
| `POST api/events/v1/intake/poll` | intake `PollAsync` | `events.admin` · `events:admin` |
| `GET …/catalogs/{c}/digests` | digests `ListAsync` | `events.admin` · `events:admin` |
| `GET …/catalogs/{c}/digests/{id}` | digests `GetAsync` | `events.admin` · `events:admin` |
| `POST …/catalogs/{c}/digests` (body `DigestSpec`) | digests `CreateAsync` → 201 | `events.admin` · `events:admin` |
| `PUT …/catalogs/{c}/digests/{id}` (body `DigestSpec`) | digests `UpdateAsync` | `events.admin` · `events:admin` |
| `DELETE …/catalogs/{c}/digests/{id}?expectedRevision=` | digests `DeleteAsync` | `events.admin` · `events:admin` |
| `POST …/catalogs/{c}/digests/{id}/preview` | digests `PreviewAsync` | `events.admin` · `events:admin` |
| `POST …/catalogs/{c}/digests/{id}/send` | digests `SendNowAsync` | `events.admin` · `events:admin` |
| `GET …/catalogs/{c}/digests/{id}/runs?cursor&limit` | digests `ListRunsAsync` | `events.admin` · `events:admin` |
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
| 2/3 | `Api*Broker` (Client), `AddEventsClient()` with `Identity` set (the person's token exchanged, else the service's own; `EventsClientCalls.AsService()`), or without it plus the be sdk's `AddOAuth2TokenExchange` or `AddOAuth2ClientCredentials` on the returned builder | the HTTP mirror above |
| fe | `createEventsClient` (`fe-events/kit`) | the same routes, under the host's proxy prefix |

- Conformance: one suite per broker in `packages/Testing`, run against the module, the client against the service
  host, and the fakes — `tests/Events.Tests.Conformance`.

## Intake (module-internal)

- `AddEventsIntake()` registers the SCFD pull: each source in `Events:Intake:Sources` names a catalog, an SCFD base
  URL, a subscription, a token client and a poll interval; the poller is a hosted service.
- One page per transaction: observations, source posts, event changes and the cursor commit together.
- SCFD wire (`GET api/subscriptions/{id}/events?cursor=&limit=`, `{ data: { data: [DeliveryEvent], next, hasMore } }`)
  is read by Events' own reader until `Harvest.Client` is on a registry (events-integration #15).

## Digests (V07)

- `DigestSpec`: `name`, `enabled`, `channel` (`telegram`, `log`), `schedule` (`days` as `monday`…, `times` as `HH:mm`,
  `timeZone` IANA), `selection` (`newSinceLastRun`, `startingWithinDays`, `deadlineWithinDays`, `statuses`, `kinds`,
  `cities`, `trackedOnly`, `stages`, `excludeStages`), `recipients` (`{ subject }` or `{ role }`, each with an optional `language`; role `member` is every
  member of the organization, another its Identity role's holders), `language`
  (`uz-Latn`, `ru`, `en`), `itemCap` (1–50), `linkTemplate` (`{id}`, `{key}`, `{catalog}`), `expectedRevision`.
  `DigestModel` adds `id`, `catalog`, `nextRunAtUtc`, `lastRunAtUtc`, `revision` and the times.
- `stages` and `excludeStages` (OE04) are the digest organization's tracking stage keys, matched by the same (issuer,
  organization) as `trackedOnly`: `stages` keeps only events in one of them, `excludeStages` drops events in any. An
  event the organization does not track reads as its workflow's default stage, so `excludeStages` drops untracked events
  only when it names that stage. No key may sit in both; a key no stage has matches nothing. Both are additive: a caller
  on an older package sends neither and selects as before, but replaces a digest without them.
- A run (`DigestRunModel`): `occurrenceAtUtc` (unique per digest), `trigger` (`schedule`, `manual`), `status` (`running`,
  `completed`, `partial`, `failed`, `empty`, `missed`), `matched`, `selected`, `eventIds`, `texts` (one per language),
  `recipients` (`subject`, `language`, `status`, `error`, `attempts`), `notes`. Recipient statuses are Identity's app
  notice words: `pending`, `sent`, `not_linked`, `muted`, `failed`.
- Texts are Telegram's HTML subset (`b`, `i`, `a href`), at most 4,096 characters; the preview returns the texts a send
  now at that instant sends.
- Seams the host binds: `IDigestDelivery` (one per channel; `AddIdentityTelegramDigestDelivery()` over Identity's
  `IAppNoticeBroker`, the `log` channel always) and `IDigestRecipientResolver` (a role's members; unbound by default, a
  role reading nothing and the run noting it). `AddIdentityDigestRecipients(o => o.Issuer = …)` binds it to Identity's
  `IMemberBroker` (Identity 10.0.32-beta+, `GET /api/identity/v1/organizations/{id}/members`, scope `identity.members.read`):
  each run reads the organization's members, `role:member` all of them, linked to the bot or not, so the Telegram channel
  records `not_linked` for those without a link; another issuer's organization, Identity's refusal or an unreachable
  instance reads nothing and the run notes why with Identity's code.
- The service binds both when `Events:Digests:Telegram:Identity` is set: the notice client (`Scope`, default
  `identity.notices.send`) and the member client (`MembersScope`, default `identity.members.read`), each its own token for
  `Resource`, the instance's notice audience, which is also its `Identity:Members:Audience`. The instance's declaration
  grants Events' client both scopes and lists both on that resource; without the member scope only role recipients fail.
- Scheduler (`AddEventDigests`, `Events:Digests`): `Enabled`, `PollInterval` (30 s), `CatchUpWindow` (6 h; an older
  occurrence is recorded `missed`), `LeaseDuration` (5 min; a dead host's run resumes after it), `MaxAttempts` (5, for
  rate limits and outages, after `Retry-After`), `LinkTemplates:{catalog}`. Each recipient's idempotency key is
  `events-digest:{runId}:{sub}`, so a resumed run sends nothing twice.

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
