# deployment/

How wow-two-sdk-beta.events's service ships. Descriptor format: wow-two-ws/conventions/deployment/descriptor/deploy-descriptor.md.
The packages ship separately, from `publish-packages.yml` (`engineering/scripts/scripts.md`).

| File | Role |
|---|---|
| `deploy.yml` | descriptor 2: one service, `events` (`/health`, 256 MB, `needs: [postgres]`, the keys volume); typed keys for the database, token auth, the console, the console's key ring and two Afisha intake sources; one site `app` (public, primary) at `/` and `/api`, the console behind sign-in on it; `dotnetEnvironment: true`, the `verify` dev check and the `migrate:` step |
| `Dockerfile` | three stages: build the frontend workspace (`fe-events`, then `apps/web`), publish `service/Events.Api` with its packages and the pages in `wwwroot`, run on `aspnet` with `curl` |
| `docker-compose.yml` | local verification: PostgreSQL (loopback `8414`), the one-shot `events-migrate` running the image's `migrate`, then the service on `http://127.0.0.1:8411` |

## Contract with Wheelhouse

- Image: `ghcr.io/<owner>/<repo>/events`, published by `.github/workflows/publish-docker-image.yml` through the shared
  `delivery.yml` of `wow-two-platform.pipelines` (pinned `v0.16.0`); CI never deploys. `v0.10.0` and later carry the
  typed setting keys (`settings.keys`) into the bundle, `v0.12.0` the `migrate:` step and the sets each item's
  image hosts; the runner needs Wheelhouse v0.5 or later. `v0.14.0` adds the linked keys (`from`) source 1 of the intake uses.
- Version: `X.Y` from the newest `planning/version-track/v{X.Y}/`; the image tag and `GET /api/system/status` report the
  same `X.Y.Z`, plus the build's commit after a `+`. Package releases (`be-v*`, `fe-v*`) never move it.
- Settings a target provides in `/app/appsettings.Target.json`: `DatabaseSettings:ConnectionString`, which Wheelhouse
  fills from the target's PostgreSQL database; `Deployment:TrustedProxies` or `Deployment:TrustedNetworks` names the
  ingress.
- Database: PostgreSQL, the target's own (`needs: [postgres]`); the module's set lives in schema `events`. `GET /health`
  answers 200 only while the database answers.
- Migrations: the image answers `dotnet Events.Api.dll migrate <verb>`, with exits `0` done · `1` refused · `2` failed ·
  `3` busy and a last `migrate-result` line. A deploy runs the bare `migrate` (apply, then seed) from the release image
  before the first service restarts, as `deploy.yml`'s `migrate:` item declares (pipelines v0.12.0); a deployed boot
  only verifies the levels and refuses to serve a database behind it. `migrate sets --json` needs no database.
- Verbs and config check: `Program.cs` runs the be sdk's `ServiceHost`. Events' own verbs (`intake`, `catalog`,
  `workflow`, `import`, `event`) run before any host boots; `migrate` and `config check` are the be sdk's. A deploy runs
  `config check` (`deploy.yml`'s `configCheck:` item) after `migrate` and before the restart: exit `0` consistent, `1`
  refused (an invalid setting, such as an `auto` source with the `filter` classifier and no `Filter`), `2` failed; the
  last line is `config-result {json}`. A deployed boot runs the same checks and refuses to serve on a finding.
- Callers: `Events:Auth:Audience` is the `app` site's URL with its trailing slash; `Events:Auth:Issuers:N:Issuer` and
  `Events:Auth:Issuers:N:Audiences:M` name each trusted Identity issuer (`MetadataAddress` or `Jwks` optional). Every
  route needs a token: a person's needs the scope and the permission, a service's the scope alone; no API keys.
  Identity's revocation signals arrive at `{audience}signals/identity`.
- Review console: `Events:Console:Authority`, `Events:Console:ClientId` (`events-console`) and
  `Events:Console:ClientSecret` turn on Identity's BFF under `/api/identity` (register the client's RedirectUris
  `{site}/api/identity/callback`, PostSignOutRedirectUris `{site}/`, SignalUrl `{site}/api/identity/signal`); the API
  then also accepts that session. Unset, the console pages serve and the API answers the browser 401. The console is
  decided at boot, from the settings the host has then. Its sessions live in the service's database (the be sdk's
  `distributed_cache` set, schema `cache`, applied by `migrate` beside `events`) and its cookies under a key
  ring in `DataProtection:KeysPath` (`/var/lib/events/keys`, the `keys` volume) named `DataProtection:ApplicationName`;
  keep both as deployed, or every reviewer is signed out. A rollback names its set: `migrate rollback --set events …`.
- Intake: `Events:Intake:Sources:N` (`Name`, `Catalog`, `BaseUrl`, `SubscriptionId`, `TokenEndpoint`, `ClientId`,
  `ClientSecret`, `Scope`, `Resource`, `Review`, `Classifier`, `Filter`, `MinConfidence`, `DefaultCountry`, `OwnerOrg`, `OwnerIssuer`, …); none polls nothing. `Review`
  `auto` publishes what the classifier marks relevant (`classifier:{name}` as the reviewer, its verdict as the reason) and
  holds the rest as candidates; `manual` keeps every record for a person. A person's review takes the event off automatic
  changes, and `POST …/review/{id}/reopen` returns any published event to review. After each poll an `auto` source maps its
  unreviewed and classifier-reviewed events again from their latest payload, so a better mapping reaches stored events
  once. `OwnerOrg` (required) pins the organization that must own the source's catalog: a catalog anyone else owns, or a
  source pinning none, pulls nothing. Intake status and `intake/poll` cover only sources whose catalogs the caller's
  organization owns. Before each pull a source reads its subscription (`GET api/subscriptions/{id}`) and pulls nothing
  unless it carries the source's `Filter` (when one is set), schema `wow2.event.v1` and state `active`; the mismatch is
  recorded on the source's cursor row and logged (4107). A post's lifecycle follows SCFD's records: a listing gone
  before its event holds an automatic, untracked event for a person (`source:{instance}`, "the source listing is gone
  before the event") and releases it when the listing returns; a post merged into another carries its event to the
  survivor, or merges into the survivor's own event when no person decided it and no organization tracks it. The token
  request names exactly one `resource`, as
  Identity requires: `Resource`, else `BaseUrl` with its slash. The worker logs `Events intake: polling N source(s)` at
  start, one Information line per page polled (records stored, quarantined, more pages) and, for an `auto` source,
  one per backlog pass (held events checked, re-mapped, published); a failed page logs a
  Warning naming its kind (`token-refused:<oauth error>`, `scfd-status-<code>`, `scfd-unreachable`, `timeout`). The image answers `dotnet Events.Api.dll intake check [--source <name>]
  [--foreign-subscription <id>]` from configuration only (no database): per enabled source, its own token pulls its
  subscription (`check=pull`, 200, with `records` and `event-records` of `wow2.event.v1`), reads it and finds the
  source's filter, schema and state (`check=subscription`, 200; `reason=filter-mismatch`, `schema-mismatch` or
  `state-<state>` otherwise), is refused the tenants list
  (`check=tenants`, 403) and, with `--foreign-subscription`, finds no other tenant's subscription (`check=foreign`, 404,
  never 403). One `intake-check source=… check=… status=… expected=… ok=…` line per check, `reason=not-configured` for a
  source missing its base URL, subscription or token client, and a last `intake-check-result ok=…`; exit 0 when every
  check matches, else 1. The last line repeats every status, for runners that keep only it:
  `intake-check-result ok=… source=… token=<ok|token-refused:<oauth error>|unreachable|timeout|not-configured> pull=…
  records=… event-records=… subscription=<ok|mismatch|status> tenants=… foreign=…`. It prints no token, header, body,
  URL or host name.
- Provisioning, as the deploy's operator for the owner organization, through Wheelhouse Run after `migrate` (no web
  host, no token; idempotent; the last line carries the outcome):
  - `dotnet Events.Api.dll catalog ensure <slug> --owner-org <org id> [--owner-issuer <issuer>] [--name <name>]
    [--kinds a,b]` creates or updates a private catalog (`--owner-issuer` defaults to `Events:Auth:Issuers:0:Issuer`):
    `catalog-ensure-result ok=… slug=… created=… changed=… revision=…`, or `ok=false reason=…` (another organization
    owns the slug, usage, no issuer, no database).
  - `dotnet Events.Api.dll workflow put --owner-org <org id> [--owner-issuer <issuer>] --default <key> --stages
    key:class,…` writes the organization's workflow: `workflow-put-result ok=… stages=… changed=… revision=…`.
  - Ocharo on dev: `catalog ensure ocharo-fashion --owner-org <Ocharo's org_id> --name "Ocharo fashion" --kinds
    runway,trade-fair,market,exhibition,competition,festival,conference,other`, then `workflow put --owner-org <id>
    --default candidate --stages candidate:open,shortlisted:open,applying:active,applied:active,confirmed:active,
    attended:won,skipped:lost,rejected:lost`. Intake pulls nothing until the catalog exists.
  - `dotnet Events.Api.dll import <catalog> --owner-org <org id> [--owner-issuer <issuer>] [--dry-run] [--file <path>]`
    onboards a tenant's own `wow2.events-import.v1` bundle, read from the file or from standard input, with the import
    API's rules (whole bundle validated first, the id map stable on every rerun): one `import-report {json}` line (counts
    and the old → new id map), then `import-result ok=… dry-run=… rows=… created=… updated=… unchanged=… failed=…`.
  - `dotnet Events.Api.dll event show <catalog> --owner-org <org id> [--owner-issuer <issuer>] (--id <event id> | --key
    <facts key> | --post <SCFD post id>)` reads one stored event back, in any review state: one `event {json}` line
    (facts, review, sources), then `event-show-result ok=true id=… review=… revision=… reviewed-by=… facts={json}`, or
    `ok=false reason=…` (usage, the catalog or the event not found; another organization sees neither).
- Imports by people: a signed-in owner or admin uploads a bundle on the console's `/imports` page, dry runs it, then
  imports. The console's sign-in asks for the BFF's defaults only (`openid profile email phone offline_access`; Identity
  grants `events.*` to client `events-console` only through token exchange). The console is Events' own app on the same
  host, so its session needs no token for Events: the session carries the five `events.*` scopes, and the person's
  `events:*` permissions from the sign-in gate each action.
- Dev check: after a dev rollout, `verify` curls `$VERIFY_SERVICE_EVENTS/health` and `/api/system/status` with
  `Host: localhost`, and refuses unless the status's `intake.status` is `ok` or `off`: a source whose last poll failed
  (a refused token, a subscription that does not match) fails the check. The status names each active source as `ok`,
  `failing` or `waiting` (before its first poll), never an error text or a host. `HostFilteringTests` replays the
  descriptor's requests.
- After each main image publish, `conformance.yml` boots the released image on PostgreSQL, migrates it and checks its
  status, its pages, a 401 from the API without a token and `migrate check`; the full conformance suite (module and
  client, tokens minted in process) runs with the tests on every build.

## Local run

- An IDE run reads `service/Events.Api/appsettings.Local.json` (gitignored; the `https` launch profile sets
  `LOCAL_SETTINGS=true`) for its connection string. `docker compose up -d database` (from this folder) starts the local
  PostgreSQL on loopback `8414`:

```json
{ "DatabaseSettings": { "ConnectionString": "Host=127.0.0.1;Port=8414;Database=events;Username=events;Password=events" } }
```

- A local run (`Development` without `DEPLOY_ENVIRONMENT`) creates the database if missing and applies pending
  migrations at boot; `dotnet run --project service/Events.Api -- migrate check` reports the levels without writing.
- Local image check:

```bash
docker build --build-arg APP_VERSION=0.1.99 --build-arg VCS_REF=$(git rev-parse HEAD) \
  -f engineering/deployment/Dockerfile -t events:local engineering/codebase
```
