# wow-two-sdk-beta.events — Capability spec

*Last updated: 2026-11-17*

> The capability's definition: what it does, for whom, in which forms. State and decisions: `context.md`; one spec per
> feature group in `features/`. Rules: wow-two-ws/conventions/development/capability-forms.md.

Reconciled 2026-11-10 (events-integration #14) from wow-two-ws `ideas/events-service-spec.md` (2026-10-27, written for
webhook intake and a `wow-two-platform.events` host) and `ideas/events-infrastructure-next.md` (2026-11-05: pull
delivery, evidence, Desk migration). What changed: SCFD delivers by pull, never webhook (#6); every caller of ours
holds an Identity token, never an API key (#3); each observation keeps its evidence; Desk's `showcase_events` moves in
through an import bundle with a stable id map; one domain repo ships every form (#2).

## Internal use

The developer, 2026-11-10 (events-integration #50): "I only ever use this sourcing for internal use, like to track
events for ocharo and not post them anywhere else".

- a catalog fed by SCFD stays private to its owner organization; Events refuses to make it `shared` or `public`, and
  intake applies nothing into a catalog that is not private.
- sourced facts and captures serve internal tracking and review; nothing sourced is republished.
- sourced images and text stay review evidence, never content: intake leaves `images` empty, and only a curator adds
  images to an event.
- calendar export and agent reads stay inside the owner organization; anonymous or token feed links are not built.
- `shared` and `public` visibility, and Form 3's shared catalog, apply only to catalogs with no sourced records.

## Problem

- Products track dated happenings — fairs, fashion weeks, exhibitions, competitions — and the deadlines before them.
- The expensive half is generic: collecting sources, deduplicating, reviewing, keeping evidence, exporting calendars.
- Building it inside Ocharo Desk means building it again for the next product; Events owns it once (E1: catalog and
  tracking here; Desk keeps its page, its organization authorization and references to Ocharo entities).

## Forms

| Form | Ships | Named consumer | State |
|---|---|---|---|
| 1 · library | `WoW2.Sdk.Beta.Be.Events.Module` | the `events` service | planned, P1 |
| 2 · self-hosted service | `…Events.Client` + the `events` image | Ocharo Desk, through its backend | planned, P1 |
| 3 · provided service | `…Events.Client` | a second product sharing a catalog with no sourced records (#50) | P2 |

## Model

| Concept | Is | Owned by |
|---|---|---|
| catalog | a curated namespace of events: slug, name, owner organization, visibility (`private` · `shared` · `public`; a catalog with sourced records stays `private`), locales, kinds | its owner organization |
| event | a reviewed fact: something that happens at a time and place | the catalog |
| observation | one record a source reported, with its evidence, waiting to become or change an event | the catalog |
| workflow | the stages and custom fields one organization tracks events with | the organization |
| tracking | one organization's private record about one event: stage, priority, notes, fields, references, history | the organization |

- an event exists whoever tracks it; tracking is private and never changes the event.
- the service knows no product's vocabulary: kinds, stages and fields are configuration.
- product entities (Ocharo products, catalogue editions, campaigns) attach to tracking by opaque reference only.
- organizations and people are keyed by issuer and id (`iss` + `org_id`, `iss` + `sub`), never by a local copy.

### Event

| Field | Rule |
|---|---|
| `id` | UUID v7 |
| `catalog`, `key` | `key` optional, `^[a-z0-9][a-z0-9-]{0,63}$`, unique per catalog |
| `title`, `summary` | localized text (`{ locale: text }`); title 1–200 per locale, at least one; `und` when the language is unknown |
| `kind`, `tags` | `kind` from the catalog's kinds; tags free and lowercase |
| `organizer` | name, website (P1 embedded; deduplicated organizers are P2) |
| `place` | venue, address, city, region, ISO-2 country, geo (P1 embedded) |
| `attendance`, `audience` | `offline` · `online` · `mixed`; `public` · `trade` · `invite-only` |
| `status` | `scheduled` · `postponed` · `rescheduled` · `cancelled` · `moved-online` |
| `starts`, `ends` | When: `value` (`2026-03-14T19:00` · `2026-03-14` · `2026-03` · `2026` · null), `precision` (`minute` · `day` · `month` · `year` · `unknown`), `certainty` (`confirmed` · `announced` · `estimated`) |
| `deadlines` | kind (`application` · `registration` · `early-bird` · `submission` · `payment` · `other`), `at` (When), label, url |
| `offers` | kind (`ticket` · `exhibitor` · `sponsorship` · `submission` · `other`), name, price min/max/currency (null when unknown, never 0), free, terms, url |
| `website`, `ticketUrl`, `onlineUrl` | http(s), ≤ 2,048 |
| `images` | curated references: url, attribution (#31: no bytes copied in P1; sourced images stay evidence, #50) |
| `sources` | every source identity that reported it: kind (`scfd` · `import` · `manual`), id, url, first and last seen; for SCFD also its name (the subscription's source name, else its group, else the intake source's), its listing's state (`live` · `suspect` · `gone` · `unmatched`) and when the source posted it |
| `review` | `candidate` · `published` · `rejected` · `archived`; consumers see `published` |
| `revision` | optimistic concurrency; `createdAt`, `updatedAt`, `publishedAt` |

- dates are honest: an unknown day is never invented; a month value covers its whole month in filters, unknown sorts
  last; an `estimated` date never becomes `confirmed` without a reviewer.

## Intake

SCFD (Source Collection and Feed Delivery, `wow-two-sdk-beta.harvest`) collects and delivers; Events consumes.

- Events pulls one provisioned subscription per catalog (`Schema` `wow2.event.v1`) with its own client-credentials
  token: `GET api/subscriptions/{id}/events?cursor=&limit=`, at least once, no global order.
- each page's observations and the next cursor commit in one transaction; a crash before the commit replays the page.
- a retry deduplicates on (subscription, delivery `EventId`); `ItemId` identifies content across resyncs.
- the highest `Revision` per `PostId` wins; an older revision arriving late is stored and marked superseded.
- a malformed record (no payload, a schema other than `wow2.event.v1`, no title, unreadable dates) is stored as
  quarantined and never blocks the page.
- lifecycle records (`link.state`, `post.merged`, `post.unmerged`, `post.purged`, `post.relisted`) change evidence,
  never a reviewed fact: a missing source page does not cancel an event; run and health records feed the status.
- `post.unmatched` (SCFD sends it once when a post it delivered no longer passes the subscription's filter) sends an
  event no person decided and no organization tracks back to review, held by `source:{instance}`, so an off-topic event
  leaves the published feed; its content never applies, and only a revision that passes the filter again
  (`post.new`, `post.changed`, `post.extracted`) returns it to its classifier, never a relist or an unmerge. A tracked
  event stays published, and its source reads `unmatched` until such a revision arrives.
- evidence per observation: SCFD instance, subscription, pipeline, `EventId`, `ItemId`, `PostId`, `Revision`, type,
  reason, observed time, each link's URL, source, capture and source fields, the mapping version when SCFD sends it,
  and the raw payload.
- review never blocks intake; a catalog that is not private takes nothing (#50).
- a sourced event's website is the first `site` content link on a host of its own: SCFD marks every in-page link
  `site`, so a link on a host of the post's own links (the aggregator, with `www.` and subdomains), a social, messenger
  or map host reads as no website, and the event keeps none; a published event re-maps after the next poll.
- a sourced place names a known Uzbek city in one Latin spelling (`Ташкент` and `Toshkent` read `Tashkent`), the
  spelling the catalog's own events use, so city filters and groups never split one city in two; a city the table does
  not know passes through as written, and the source's spelling stays in the observation's raw payload.
- each source has a review policy (#53, the developer: "why someone needs to accept anything ? we'll use ai
  classification ofc"): `auto` publishes what its classifier marks relevant into the private catalog and holds the rest
  as candidates; `manual` keeps every record for a person. The classifier is `filter` (SCFD's subscription filter passed
  the record) or `payload` (the record's own `classification`: classifier, verdict, confidence, reason), so an AI
  relevance step needs no rework here. The classifier and its verdict are recorded as the reviewer and the reason.
- a source's `DefaultCountry` is the operator's knowledge of the source, so it covers the cities the source names
  (`DefaultCities`, Uzbekistan's regional centres for the Afisha sources): a record naming another city (a foreign fair
  announced on a UZ source) keeps no country, a record naming no city takes the source's, and a country the record names
  stands. A source with no list gives the country to every record.
- a sourced event takes the deadlines SCFD states (`deadlines[]`: an application deadline a reader found) at their own
  precision, so "deadlines first" lists it; one with an unreadable date is left out. Its time zone is the source's
  `DefaultTimeZone` when the start's stated offset is that zone's own on that date, and none otherwise: a guessed zone
  would move the event's hour.
- a sourced event's dates carry SCFD's `certainty`: `estimated` (an AI-estimated date of an undated post) reads as an
  estimated date, shown as approximate and never as announced; `stated`, or none on an older wire, reads as announced.
  A later revision that states the day replaces the estimate.
- a sourced event's kind, in order: the classifier's, when the catalog's taxonomy holds it; the kind the record itself
  names; the source's `DefaultKind` (an exhibitions feed reads `exhibition`), when the record maps to `other` and the
  taxonomy holds it; else `other`. A reviewer's kind stands over all of them.

## Review

- a new source identity becomes a `candidate` event, published at once under `auto` review when its classifier marks it
  relevant; a newer revision of a candidate updates it in place.
- a newer revision of a published event becomes a pending change: reviewers see its proposed fields beside the
  current ones and their evidence; a source never overwrites a reviewer's accepted value, a stage or a note.
- reviewers accept (publish, with edits), reject, merge a candidate into an existing event (sources and evidence move,
  the candidate archives), apply or dismiss a pending change, and reopen a published event for review.
- an event the classifier published follows its source's newer revisions by itself until a person reviews it; from
  then on a newer revision waits as a pending change.
- curators create and edit published events by hand; a curator's own entry is reviewed by definition.
- members of the owner organization propose events (`POST …/catalogs/{c}/candidates`, `events.track`). In a private
  catalog the owner organization is the only reader, so a proposal publishes at once, reviewed by the proposer
  (`person:{sub}`, "owner-organization proposal in a private catalog"), and the organization's lists and tracking show
  it right away (OE03, 2026-11-19: Ocharo's "Add event" had vanished, as no Ocharo role can accept a candidate in the
  app). In a shared or public catalog a proposal stays a candidate and publishes only when a reviewer accepts it.
- the review console is Events' own page set; reviewers sign in through the catalog owner's Identity (P1: Ocharo's)
  and need `events:review` in the owner organization.

## Tracking

- a workflow per organization: stages (key, localized label, class `open` · `active` · `won` · `lost`), a default
  stage and custom fields; the first read creates the default workflow.
- tracking per (organization, event): stage, priority (`high` · `normal` · `low`), assignee, notes (≤ 8,000), fields,
  references (type and id pairs), revision; every stage change lands in its history with actor and time.
- the summary counts tracking per stage and lists the nearest open deadlines.

## Imports

`wow2.events-import.v1`: one bundle moves a product's own events into a catalog and its tracking (#26).

- `POST …/catalogs/{c}/imports` with `dryRun`; the whole bundle validates before any write.
- each row carries the old id (`legacyId`), so a rerun updates nothing twice and returns the same old → new map.
- dates keep their precision: a month stays a month, `estimated` stays estimated; a row's `language` is kept, `und`
  when absent; titles are never assumed to be in one language.
- the row's stage, priority, notes, participation and cost land in the importing organization's tracking; its source
  URL and website in the event's sources; the producer's whole row (`legacy`) is kept as the row's evidence.

## Calendar

- `GET …/catalogs/{c}/events.ics` exports published events for a filter, with stable UIDs and sequence numbers from
  the revision; an exact deadline exports as its own all-day entry. The export reads as a catalog read: inside the
  owner organization (#50); token feed links are not built.

## Digests

- An organization defines digests of a catalog it reads (`events.admin`): a name, a schedule (days and local times in an
  IANA zone, such as Monday 09:00 `Asia/Tashkent`), a selection, recipients, a language (`uz-Latn`, `ru`, `en`), an item
  cap and a channel. Any product embeds the editor (`fe-events/admin` `DigestsPanel`) for its catalog.
- Selection, as of the occurrence's local day: an event goes in when it matches a rule — new since the previous run,
  starting within N days, an open deadline within N days — and every filter (statuses, kinds, cities, tracked only,
  the organization's tracking stages to include or leave out). An untracked event counts as the workflow's default stage,
  so a digest leaving out `skipped` still lists it and one leaving out the default stage does not.
  Published events only; an event that ended never goes out; month and year dates count by their whole span.
- Each occurrence runs once (a run row unique on digest and occurrence) and renders one message per recipient language:
  the title, the dates at their precision, the city, the nearest open deadline and a link from the digest's URL template
  (or the host's per catalog). A preview renders the same text without sending; a send now runs outside the schedule.
- Channels sit behind one seam (`IDigestDelivery`): Telegram through Identity's app notices (Identity owns the bot, apps
  never talk to it; each recipient's message carries the run's idempotency key) and the host's log; email and webhooks
  can follow. Run history keeps the selected count and each recipient's `sent`, `not_linked`, `muted` or `failed`.
- Recipients are Identity `sub`s, or an organization role resolved at each run through the host's
  `IDigestRecipientResolver`: bound to Identity's organization members (`AddIdentityDigestRecipients`), `role:member` reads
  every member and `role:editor` (any catalog role) its holders; a member with no Telegram link reads `not_linked`.

## Access

| Scope (app) | Permission (person) | Opens |
|---|---|---|
| `events.read` | `events:read` | a catalog's published events; the organization's tracking reads |
| `events.track` | `events:track` | tracking and workflow writes |
| `events.review` | `events:review` | review, curation |
| `events.import` | `events:admin` | imports |
| `events.admin` | `events:admin` | catalogs, intake status, digests |

- a person's call needs both; a service's call its scope alone (`api-communication.md`).
- record rules run on Events' own rows: a private catalog reads only for its owner organization; tracking is keyed by
  the caller's organization, and another organization's route answers 404.
- Identity's two signed signals arrive at `{Uri}signals/identity` and refuse a revoked person's tokens until expiry.

## Phases

- P1 (v0.1): the contract, module, client, service, review console, fe package, imports, ICS, the SCFD pull consumer,
  conformance and deploy; on dev a live Afisha record becomes a candidate and Desk's Events page reads through Events.
- P2: deduplicated places, organizers and series, occurrences, cross-source match proposals, sharing of unsourced
  catalogs, curated images in a Files instance, reminders, webhooks, MCP tools inside the owner organization,
  translations, sources API, push intake — `backlog.md`.

## Sources

- wow-two-ws `system/sessions/events-integration/events-integration.md` — the integration's contracts #14–#33.
- wow-two-ws `ideas/events-service-spec.md`, `ideas/events-infrastructure-next.md` — the inputs reconciled here.
- wow-two-ws `conventions/development/backend/dotnet/core/hla/api-communication.md` — the auth rules.
- `ocharo-ws` `docs/app-boundaries.md` E1 — what Events owns and what Desk keeps.
