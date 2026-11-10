# codebase/

The code lives in `events.backend-services/` (.NET) and `events.frontend-services/` (Vue).

## Backend

- `Events.BackendServices.slnx` groups `Packages/`, `Services/` and `Tests/`.
- `packages/` holds the NuGet family `WoW2.Sdk.Beta.Be.Events.*`: `Abstractions` (the contract), `Module` (Form 1,
  with `Migrations/NNN-name/{Apply,Rollback}.sql`, the named PostgreSQL set `events` in schema `events`; its
  `001-baseline` creates no table, so a domain's first table is `002-…`), `Client` (Forms 2/3) and `Testing` (fake +
  conformance suite).
  `Directory.Build.props` makes them packable by path; the core pin in `Directory.Packages.props` is their floor.
- `service/Events.Api` hosts the module by `ProjectReference`: `AddApiDefaults()` / `UseApiDefaults()`, the module's
  set claimed as `service`, `GreetingsController` (the HTTP mirror of the contract), `SystemController`, and the pages in
  `wwwroot/` (filled by the `BuildSpa` target; `-p:BuildSpa=false` skips it). `Program.cs` answers `migrate <verb>`
  (`check`, `apply`, `sets --json`, …) before any startup work: the deploy's migrate step. A local run applies at
  boot; any other boot only verifies the levels.
- Tests: `Unit` (package logic), `Integration` (the module over PostgreSQL, its set in its own schema), `E2E` (the host
  in process over PostgreSQL: wire, pages, `/health`, the descriptor's dev check, the image's `migrate` verbs on the verb
  harness), `Conformance` (one suite against module, client and fake), `Migrations` (required: the module's set —
  apply, Apply → Rollback → Apply, the be sdk lint, the idempotency proof). All but `Unit` need Docker. Run them with
  `dotnet test Events.BackendServices.slnx`.

## Frontend

A pnpm workspace: `packages/fe-events` (`@wow-two-beta/fe-events`, published: `./contracts`, `./kit`, `./admin`) and
`apps/web` (hosted pages and the console that composes `./admin`, private). `scripts/deploy.mjs` copies
`apps/web/dist/` to `events.backend-services/service/Events.Api/wwwroot/`.

Rules: wow-two-ws/conventions/development/repo/structure/domain-structure.md
