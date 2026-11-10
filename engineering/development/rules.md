# wow-two-sdk-beta.events — Engineering Rules

*Last updated: 2026-11-10*

> Working rules for agents in this repo — repo-specific deltas only. Shared rules: wow-two-ws/conventions/.

- Every broker call acts for the current principal; record rules (catalog visibility, owner organization, tracking
  isolation) live in the module, never only in a controller.
- Nothing a source reports publishes without a reviewer: intake creates candidates and pending changes only.
- A source never overwrites a reviewed fact, a stage or a note; it proposes.
- Dates stay honest: never invent a day, never turn `estimated` into `confirmed`.
- Intake commits a page's observations and its cursor in one transaction; a malformed record is quarantined, never
  thrown.
- Host addresses, tokens and secrets stay out of the repo; Wheelhouse types them per target.
