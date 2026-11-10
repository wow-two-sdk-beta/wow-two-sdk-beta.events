# wow-two-sdk-beta.events — Product Context

*Last updated: 2026-11-10*

> Current state — changes often. Durable definition: `product.md`.

## Current state

Repository created 2026-11-10 under the Events integration (wow-two-ws `system/sessions/events-integration/`). v0.1
(P1) is built: contract, module (catalogs, review, SCFD intake, tracking, imports, calendar), client, service, review
console and `fe-events`, 323 tests. Image `v0.1.1` is released; the `events-dev` rollout is the wheelhouse lane's; the
NuGet family and npm package wait for the first publish (the developer's NuGet policy and npm first version). Picks per contract: `engineering/planning/backlog.md` § Integration
picks.

## Active tasks

| Task | Status | Notes |
|---|---|---|
| v0.1 build | done, image `v0.1.1` | `engineering/planning/version-track/v0.1/v0.1.md` |
| NuGet policy, npm first version | todo | the developer's hand steps (the repo is public since 2026-11-10) |
| Registration in Ocharo's Identity | todo | platform lane (events-integration #21, #30) |
| Dev deploy | todo | wheelhouse lane (events-integration #28) |

## Open questions

- #31 images: references only in P1, bytes into FSS on publish in P2 — a proposal waiting for the platform and
  wheelhouse lanes' input.

## Decisions

- 2026-11-10 — Events reads SCFD's wire with its own reader until SCFD publishes `Harvest.Client` (events-integration
  #15): the package is on no registry.
- 2026-11-10 — PostgreSQL only, with Testcontainers in every tier but unit: the domain template's current shape.
