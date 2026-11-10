# wow-two-sdk-beta.events — Package registry

*Last updated: 2026-11-10*

> Every package this repo publishes, one version line per registry. Layer rules:
> wow-two-ws/conventions/development/repo/structure/domain-structure.md § Layers.

| Package | Layer | References | Heavy deps | Forms | Consumers |
|---|---|---|---|---|---|
| `WoW2.Sdk.Beta.Be.Events.Abstractions` | contract | the kernel `WoW2.Sdk.Beta.Be.Abstractions` only; no framework reference | none | all | products' Domain/Application projects; Ocharo Desk's backend |
| `WoW2.Sdk.Beta.Be.Events.Module` | forms | Abstractions, core | none | 1 | `service/Events.Api` |
| `WoW2.Sdk.Beta.Be.Events.Client` | forms | Abstractions, core | none | 2, 3 | Ocharo Desk's backend (events-integration #25) |
| `WoW2.Sdk.Beta.Be.Events.Testing` | test kit | Abstractions, `WoW2.Sdk.Beta.Be.Testing` | xunit (via be Testing) | all | test projects |
| `@wow-two-beta/fe-events` | fe | peers `@wow-two-beta/fe` `>=0.0.n <0.1.0`, `vue` | none | all | Ocharo Desk's web app, `apps/web` |
