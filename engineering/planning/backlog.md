# wow-two-sdk-beta.events — Backlog

*Last updated: 2026-11-10*

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
| #15 | SCFD pull, consumer side | provided, with one gap | P1 | Events polls `GET api/subscriptions/{id}/events?cursor=&limit=` (no ack, no long-poll) with its client-credentials token; the subscription is provisioned (`Events:Intake:Scfd:SubscriptionId`) with `Schema` `wow2.event.v1`; the page's observations and the next cursor commit in one transaction; dedup on (subscription, `EventId`), highest `Revision` per `PostId`; gap: SCFD's `Harvest.Abstractions` and `Harvest.Client` are on no registry, so P1 reads the wire through Events' own reader behind `IObservationFeed`, swapped for `Harvest.Client` once published (P2 row below) |
| #19 | Evidence, consumer side | change | P1 | each observation keeps the SCFD instance, subscription, pipeline (from settings; the event carries none), `EventId`, `ItemId`, `PostId`, `Revision`, type, reason, `OccurredAtUtc`, every link's `Url`, `SourceId`, `CaptureId`, `CapturedAtUtc` and `SourceFields`, and the raw payload; asks of SCFD: the extraction stamp (norm and rules versions) in the `wow2.event.v1` projection, and `PipelineId` on `DeliveryEvent`; until then Events stores the mapping version as unknown |
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
| Catalog and events | planned | [product](../../product/product.md#model) |
| Intake from SCFD | planned | [product](../../product/product.md#intake) |
| Review | planned | [product](../../product/product.md#review) |
| Tracking and workflows | planned | [product](../../product/product.md#tracking) |
| Imports | planned | [product](../../product/product.md#imports) |
| Calendar export | planned | [product](../../product/product.md#calendar) |
| Review console | planned | [product](../../product/product.md#review) |
| Developer kits | planned | [contract](../architecture/contract.md) |

## Intake

| Item | Type | Notes |
|---|---|---|
| Swap Events' SCFD wire reader for `Harvest.Client` once SCFD publishes its NuGet family | engineering | P2; events-integration #15 |
| Mapping version from SCFD's extraction stamp, once the projection carries it | feature | P2; events-integration #19 |
| Several SCFD subscriptions per catalog, managed through a sources API | feature | P2; P1 reads one subscription from settings |
| Webhook push from SCFD | feature | P2, waits for a consumer that needs it; events-integration #6 |
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
