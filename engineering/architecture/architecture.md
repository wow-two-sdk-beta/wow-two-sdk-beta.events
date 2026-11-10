# wow-two-sdk-beta.events — Architecture

*Last updated: 2026-11-10*

One contract, two registrations, one service that hosts the first.

```
product Domain / Application ──► Abstractions (contract)
product composition root ──► Module (Form 1)  or  Client (Forms 2/3)
service/Events.Api ──► Module (ProjectReference), + HTTP mirror of the contract + hosted pages (apps/web)
Client ──HTTP──► service/Events.Api ──► Module ──► the service's store
Module (intake) ──HTTP pull──► SCFD subscription feed
```

| Form | Registration | Store owner |
|---|---|---|
| 1 · library | `services.AddEvents()` + the module's migrations | the consumer |
| 2 · self-hosted | `services.AddEventsClient(…)` + a `events` container the consumer runs | the consumer's container |
| 3 · provided | `services.AddEventsClient(…)` | the central instance |

| Project | Responsibility |
|---|---|
| `packages/Abstractions` | the contract — eight brokers (catalogs, events, review, tracking, workflows, imports, calendar, intake), models, scopes, permissions, routes, errors; no DI, options or I/O |
| `packages/Module` | Form 1 over PostgreSQL schema `events`; embedded SQL migrations; `AddEventsIntake()` adds the SCFD pull consumer |
| `packages/Client` | Forms 2/3 over the service's HTTP API |
| `packages/Testing` | the conformance suite of every broker and a settable test principal |
| `service/Events.Api` | hosting, the HTTP mirror of the contract, the hosted pages and console |
| `fe-events` | the TypeScript contract mirror and client (`./contracts`, `./kit`), product components (feed, card, deadlines, detail, tracking form), the reviewer's screens (`./admin`) |
| `apps/web` | the review console (`/`, composing `fe-events/admin`), signed in through Identity's BFF |
