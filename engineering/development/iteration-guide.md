# wow-two-sdk-beta.events — Iteration Guide

*Last updated: 2026-11-10*

Repo-specific deltas to wow-two-ws/conventions/planning/version-track/version-track.md.

- Plan: an iteration is a row group in the active `planning/version-track/v{X.Y}/v{X.Y}.md`; the backlog row moves
  there when the iteration starts.
- Implement: contract first (`packages/Abstractions` plus its conformance case in `packages/Testing`), then the module,
  then the client and the service's HTTP mirror, then `fe-events`; a schema change is a new migration pair.
- Verify: `dotnet test engineering/codebase/events.backend-services/Events.BackendServices.slnx` (Docker) and the fe
  workspace's `pnpm lint && pnpm test && pnpm build && pnpm check:package`; an intake change also passes the named
  intake tests (replay, out-of-order revisions, crash around the cursor commit, malformed record).
- Close: tick the iteration in the version track in the commit that builds it (events-integration #42).
