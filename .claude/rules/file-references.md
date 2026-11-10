# File References

> **Lookup table only.** Do NOT read files proactively — only when the current task requires them.
> **Tracks docs outside `engineering/codebase/`.** Source files are navigated via `tree`/`find`/`grep`, never listed here.

| Question about | Read |
|---|---|
| The capability — problem, forms, model, phases | `product/product.md` |
| Name and one-line description (read by consoles) | `product/product.json` |
| State, tasks, decisions | `product/context.md` |
| Feature list + state | `engineering/planning/backlog.md` § Features |
| One feature's spec | `product/features/{feature}.md` |
| User / product flows | `product/flows/flows.md` |
| Hosted pages' brand | `product/brand/brand.md` |
| Technical overview · stack · map | `engineering/engineering.md` |
| System design — forms map, package graph, service role | `engineering/architecture/architecture.md` |
| The broker contract, every form | `engineering/architecture/contract.md` |
| Packages, layers, consumers | `engineering/architecture/package-registry.md` |
| Architecture decisions | `engineering/architecture/decisions/decisions.md` |
| What lives where in the code | `engineering/codebase/codebase.md` |
| Backend dev guidelines | `engineering/development/backend-guidelines.md` |
| Frontend dev guidelines | `engineering/development/frontend-guidelines.md` |
| Iteration / version workflow | `engineering/development/iteration-guide.md` |
| Backlog — every unbuilt item | `engineering/planning/backlog.md` |
| Active version | newest `engineering/planning/version-track/v{X.Y}/v{X.Y}.md` |
| Working rules for agents | `engineering/development/rules.md` |
| Repository — how it was created, GitHub settings, rollout state | `engineering/development/development.md` § Repository |
| Deploy / ops | `engineering/deployment/deployment.md` |
| What deploys — service, sites, settings, dev check | `engineering/deployment/deploy.yml` |
| Releases — versions, tags, verification | `engineering/scripts/scripts.md` |
| Research and analysis batches | `engineering/research/research.md` |

## Source projects (`engineering/codebase/`)

> Individual files NOT listed — use `tree`/`find`/`grep`. Describe projects only.

### `codebase/events.backend-services/`
| Project | What it is |
|---|---|
| `packages/Abstractions` | the contract: the catalogs, events, review, tracking, workflows, imports, calendar and intake brokers, models, scopes, routes, errors |
| `packages/Module` | Form 1: the broker in process, `AddEvents()`, the named PostgreSQL set `events` of embedded SQL migrations |
| `packages/Client` | Forms 2/3: the broker over the service's HTTP API, `AddEventsClient()` |
| `packages/Testing` | fake broker and the conformance suite |
| `service/Events.Api` | the deployable host: the module, the HTTP mirror, the hosted pages |
| `tests/Events.Tests.{Unit,Integration,E2E,Conformance,Migrations}` | the test tiers |

### `codebase/events.frontend-services/`
| Project | What it is |
|---|---|
| `packages/fe-events` | `@wow-two-beta/fe-events`: `./contracts` mirror, `./kit` (client, dates, feed and tracking components), `./admin` (review queue, observations, intake status) |
| `apps/web` | the review console (`/`, composing `./admin`), served by `Events.Api` |
