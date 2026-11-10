# wow-two-sdk-beta.events — Product Context

*Last updated: 2026-11-10*

> Current state — changes often. Durable definition: `product.md`.

## Current state

Repository created 2026-11-10 under the Events integration (wow-two-ws `system/sessions/events-integration/`). v0.1
(P1) is built: contract, module (catalogs, review, SCFD intake, tracking, imports, calendar), client, service, review
console and `fe-events`. CI releases the image, NuGet and npm on each push to `main` (versions per release in the version
track); the service runs on `events-dev`, and its rollout and provisioning (`catalog ensure`, `workflow put`, `import`) are the wheelhouse lane's. Picks per contract: `engineering/planning/backlog.md` § Integration
picks.

## Active tasks

| Task | Status | Notes |
|---|---|---|
| v0.1 build | done | releases per version in `engineering/planning/version-track/v0.1/v0.1.md` |
| npm first version | done | `@wow-two-beta/fe-events` `0.0.3` (0.0.1 by hand, 0.0.2 and 0.0.3 by CI) |
| Registration in Ocharo's Identity | done | the resource, scopes and console client work on `events-dev` (events-integration #21, #30) |
| Dev deploy | done | `events-dev` on Wheelhouse (events-integration #28) |

## Open questions

- #31 images: references only in P1, bytes into FSS on publish in P2 — a proposal waiting for the platform and
  wheelhouse lanes' input.

## Decisions

- 2026-11-10 — Events reads SCFD's wire with its own reader until SCFD publishes `Harvest.Client` (events-integration
  #15): the package is on no registry.
- 2026-11-10 — PostgreSQL only, with Testcontainers in every tier but unit: the domain template's current shape.
