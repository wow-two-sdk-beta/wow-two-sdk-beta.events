# flows/

One `{flow}-flow.md` per technical flow; the product flows are in `product/flows/flows.md`.

| Flow | Path |
|---|---|
| Intake page | poller → `IObservationFeed` (SCFD `GET api/subscriptions/{id}/events`) → one transaction: observations, source posts, event changes, cursor → commit |
| Person's call | product backend → token exchange at the product's Identity (`events.*` scopes, audience Events) → `Events.Api` checks scope plus `events:*` permission → broker acts for (`iss`, `org_id`) |
| Console | browser → Identity BFF session on Events' `app` site → same-origin `api/events/v1/…` with the session as a person principal |
| Revocation | Identity signs a signal → `POST signals/identity` → the deny list refuses that person's tokens until they expire |
