# wow-two-sdk-beta.events — Backlog

*Last updated: 2026-11-17*

Every unbuilt item, grouped; top of each group = next. Shipped work lives in [`version-track/`](version-track/).
Shape: wow-two-ws/conventions/planning/version-track/version-track.md

- Source: [the capability spec](../../product/product.md), reconciled 2026-11-10 from wow-two-ws
  `ideas/events-service-spec.md` and `ideas/events-infrastructure-next.md` under the Events integration
  (wow-two-ws `system/sessions/events-integration/events-integration.md`, cited `events-integration #N`).
- `P1` · `P2` in Notes is the integration's phase (P1: the Afisha → review → Desk slice on dev; P2: the rest of the
  vector), not a version. Items the active version holds are in its doc, not here.

## Integration picks

The events lane's verdict and phase for each contract it owns (events-integration #12, #35), 2026-11-10.

| # | Contract | Verdict | Phase | Pick |
|---|---|---|---|---|
| #14 | Spec reconciled | change | P1 | the reconciled spec is [`product/product.md`](../../product/product.md): pull intake from SCFD, Identity tokens, evidence per observation, the Desk import; the old webhook intake, API keys for our own callers and the `wow-two-platform.events` host drop |
| #15 | SCFD pull, consumer side | provided, with one gap | P1 | Events polls `GET api/subscriptions/{id}/events?cursor=&limit=` (no ack, no long-poll) with its client-credentials token; the subscription is provisioned (`Events:Intake:Sources:N:SubscriptionId`) with `Schema` `wow2.event.v1`; the page's observations and the next cursor commit in one transaction; dedup on (subscription, `EventId`), highest `Revision` per `PostId`; gap: SCFD's `Harvest.Abstractions` and `Harvest.Client` are on no registry, so P1 reads the wire through Events' own reader behind `IObservationFeed`, swapped for `Harvest.Client` once published (P2 row below) |
| #19 | Evidence, consumer side | change | P1 | each observation keeps the SCFD instance, subscription, pipeline (from settings; the event carries none), `EventId`, `ItemId`, `PostId`, `Revision`, type, reason, `OccurredAtUtc`, every link's `Url`, `SourceId`, `CaptureId`, `CapturedAtUtc` and `SourceFields`, and the raw payload; asks of SCFD: the extraction stamp (norm and rules versions) in the `wow2.event.v1` projection, and `PipelineId` on `DeliveryEvent`; Events reads the stamp as the mapping version (`ScfdEventMapper.cs`) and stores unknown when absent |
| #21 | Names in Ocharo's Identity | add | P1 | resource `events`, `Uri` `https://events.{domain}/` with its slash, `SignalUrl` `{Uri}signals/identity`; scopes `events.read`, `events.track`, `events.review`, `events.import`, `events.admin`; organization permissions `events:read`, `events:track`, `events:review`, `events:admin`; Desk's backend grant: `events.read events.track events.import` by token exchange; the console client `events-console` (#30) |
| #23 | Policies | add | P1 | a policy per scope; a person's call needs the scope plus its permission (`events.read` + `events:read`, `events.track` + `events:track`, `events.review` + `events:review`, `events.import` and `events.admin` + `events:admin`), a service's call its scope alone; record rules on Events' rows: tracking keys by the caller's (`iss`, `org_id`) and another organization's route answers 404; a private catalog reads only for its owner organization; review, curation and imports need the owner organization |
| #25 | Client and fe package | change | P1 | `IEventsClient` becomes the broker family in `Events.Abstractions` (catalog, review, tracking, workflow, imports, intake status, ICS) that Module and Client implement under one conformance suite; `AddEventsClient()` takes the be sdk's `AddOAuth2TokenExchange` for a person or `AddOAuth2ClientCredentials` for a service; npm `@wow-two-beta/fe-events`: contract mirror, client, date formatting, ICS and the feed, card, deadline, detail, tracking and review components, calling the host's proxy routes; NuGet from `be-v*`, npm from `fe-v*` |
| #26 | Import bundle | add | P1 | `wow2.events-import.v1`, below in § Imports; `dryRun`; idempotent on (catalog, organization, `legacyId`), so a rerun returns the same id map; month precision and `estimated` kept; language per row, `und` when unknown |
| #28 | Deploy | add | P1 | `deploy.yml`: product and service `events`, PostgreSQL (`needs: [postgres]`), site `app` (API, pages and console), typed issuer, audience, SCFD base URL, token endpoint, client id and secret, subscription id; signal route `/signals/identity`; dev check on health and status |
| #30 | Review console | add | P1 | Events' own pages on the `app` site; reviewers sign in through Ocharo's Identity with Identity's BFF as confidential client `events-console` (code with PKCE) and need `events:review` in the catalog's owner organization; platform registers the client, wheelhouse types its redirect URIs and secret; reviewers from other issuers wait for P2 |
| #31 | Event images | change | P1 references, P2 curated bytes | P1: no bytes copied; sourced images stay evidence in their observation and never become content (#50); only a curator adds an image reference. P2: curated images in a Files instance, not Ocharo's tenant (#48 open) |
| #24 | Service tokens (check) | provided | P1 | be sdk `10.0.111-beta` `Http/Auth` ships `AddOAuth2ClientCredentials` and `AddOAuth2TokenExchange` (with `IOAuth2SubjectTokenSource`); Identity `Client`'s BFF feeds a person's token (`BffSubjectTokenSource`); no Identity or core change (no #40 blocker) |

## Features

| Feature | State | Spec |
|---|---|---|
| Catalog and events | built | [product](../../product/product.md#model) |
| Intake from SCFD | built | [product](../../product/product.md#intake) |
| Review | built | [product](../../product/product.md#review) |
| Tracking and workflows | built | [product](../../product/product.md#tracking) |
| Imports | built | [product](../../product/product.md#imports) |
| Calendar export | built | [product](../../product/product.md#calendar) |
| Review console | built | [product](../../product/product.md#review) |
| Developer kits | built | [contract](../architecture/contract.md) |

## Specced next

ChatGPT builds these in the pull order of wow-two-ws `system/sessions/events-integration/tasks/tasks.md`; each row's spec is its id in the named file there. Delete a row when it ships, with any older row it covers.

| Item | Type | Notes |
|---|---|---|
| E01 Pull all five default Ocharo SCFD subscriptions into Events | fix | spec in `tasks-events-path.md` |
| E04 Hold events whose SCFD post stopped matching | fix | spec in `tasks-events-path.md` |
| E33 Show sourced city names in the app's script | fix | spec in `tasks-events-path.md` |
| E12 Give sourced events a kind from their subscription | feature | spec in `tasks-events-path.md` |
| E06 Stop taking aggregator pages as the event website | fix | spec in `tasks-events-path.md` |
| E08 Map SCFD deadlines and time zones into event facts | feature | spec in `tasks-events-path.md` |
| E11 Keep foreign cities out of the default country | fix | spec in `tasks-events-path.md` |
| E15 Expose source name, state and posted time on events | feature | spec in `tasks-events-path.md` |
| E26 Ship a conformance-tested in-memory Events fake | engineering | spec in `tasks-events-path.md` |
| E28 Replay SCFD's new golden pages in Events | engineering | spec in `tasks-events-path.md` |
| E20 Discover intake subscriptions instead of per-index settings | feature | spec in `tasks-events-path.md` |
| E21 Add country lists and untracked counts to tracking reads | feature | spec in `tasks-events-path.md` |
| E23 Let catalog members read intake freshness | feature | spec in `tasks-events-path.md` |
| E17 Attach cross-source duplicates to one event on intake | feature | spec in `tasks-events-path.md` |
| E18 Move tracking to the survivor when SCFD merges posts | fix | spec in `tasks-events-path.md` |
| O14 WoW2 Events prod target `events-prod` | engineering | spec in `tasks-platform.md` |
| V01 Review console on AppFrame and the SDK session | engineering | spec in `tasks-platform.md` |
| V02 Places and organizers as deduplicated entities | feature | spec in `tasks-platform.md` |
| V03 Cross-source match proposals | feature | spec in `tasks-platform.md` |
| V04 Custom field schemas per workflow | feature | spec in `tasks-platform.md` |
| V05 Webhooks for published, changed, cancelled and deadline events | feature | spec in `tasks-platform.md` |
| V06 Calendar export with VTIMEZONE blocks | fix | spec in `tasks-platform.md` |
| V07 Scheduled event digests with a delivery seam | feature | spec in `tasks-platform.md` |
| A07 Events takes SCFD's AI classification | engineering | spec in `tasks-platform.md` · owner: ev-ai lane |
| E32 Decide a per-source policy for event thumbnails | product | spec in `tasks-events-path.md` |

## Intake

| Item | Type | Notes |
|---|---|---|
| Swap Events' SCFD wire reader for `Harvest.Client` once SCFD publishes its NuGet family | engineering | P2; events-integration #15 |
| Switch a source's `Classifier` to `payload` | engineering | the classifier is built (`1d14977`); waits for SCFD's AI step to write `classification` (events-integration #53) |
| Several SCFD subscriptions per catalog, managed through a sources API | feature | P2; sources are settings today (`Events:Intake:Sources:N:*`, two on dev) |
| Webhook push from SCFD | feature | P2, waits for a consumer that needs it; events-integration #6 |
| Widen the stored mapping version past 64 characters | engineering | with SCFD's AI step: a model stamp added to `{schema, norm, rules, parser}` would be cut (`ScfdEventMapper.MappingVersion`); services sweep #9 |
| Swap Events' BFF session reader for the fe sdk's strategy | engineering | unblocked by fe sdk `0.0.44` (`BffOrganization` carries `name` and `slug`; a hand-built `BffUser` in a fake session adds both); done with the console's UI round, which adopts the shared `AppFrame` (events-integration #59; fe sdk `engineering/architecture/app-frame-migration.md`) |
| Operator pause, resume, replay from a cursor and resync | feature | P2; P1 exposes cursor lag and quarantine; events-integration #33 |

## Catalog

| Item | Type | Notes |
|---|---|---|
| Places, organizers and series as deduplicated entities; occurrences; parent events | feature | P2; P1 embeds place and organizer in the event |
| Cross-source match proposals (edition, title, place, dates) | feature | P2; P1 matches by source identity only |
| Catalog sharing with chosen organizations, and public catalogs, for catalogs with no sourced records | feature | P2; EV1, `ocharo-fashion` stays private (events-integration #7, #50) |
| Curated event images in a Files instance | feature | P2; sourced images stay evidence (events-integration #31, #50); the instance stays open (#48) |
| Translations of titles and summaries | feature | P2; events-integration #11 |

## Tracking

| Item | Type | Notes |
|---|---|---|
| Custom field schemas per workflow, validated | feature | P2; P1 stores fields as JSON |
| Reminders on deadlines | feature | P2; EV4, events-integration #11 |
| `VTIMEZONE` blocks in the calendar export for zoned local times | feature | P2; P1 writes `TZID` with no block, which most clients resolve from IANA |
| Feed links inside the owner organization | feature | P2; no anonymous or token URLs for sourced catalogs (events-integration #50) |

## Access

| Item | Type | Notes |
|---|---|---|
| Reviewers from issuers other than Ocharo's Identity | feature | P2; events-integration #4, #30 |
| Service callers bound to catalogs and organizations by configuration | feature | P2; P1 has no service caller of Events |

## Integrations

| Item | Type | Notes |
|---|---|---|
| Webhooks `event.published`, `event.changed`, `event.cancelled`, `deadline.approaching` | feature | P2 |
| Read-only MCP tools inside the owner organization: list, get, summary | feature | P2; events-integration #50 |
