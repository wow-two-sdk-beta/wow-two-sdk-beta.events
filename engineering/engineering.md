# engineering/

The HOW of wow-two-sdk-beta.events — architecture, codebase, development, deployment, planning, research, scripts.

| Folder | Holds |
|---|---|
| `architecture/` | the forms map, the broker contract and its HTTP mirror, the package registry, decisions, technical flows |
| `codebase/` | the backend solution (`Events.BackendServices.slnx`) and the fe workspace |
| `development/` | repo-specific guidelines, rules, the iteration guide, the repository's creation and rollout |
| `deployment/` | `deploy.yml`, the Dockerfile, local compose, the Wheelhouse contract |
| `planning/` | the backlog and the version track |
| `research/` | spikes and analyses |
| `scripts/` | release helpers `publish-packages.yml` calls |

- Stack: .NET 10, EF Core over PostgreSQL (schema `events`, embedded SQL migrations), the be sdk and Identity's `Auth`
  and `Client` packages; Vue 3 and the fe sdk for `fe-events` and the console.
