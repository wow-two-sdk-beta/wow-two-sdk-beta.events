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
  `delivery.yml` of `wow-two-platform.pipelines` (pinned `v0.12.0`); CI never deploys. `v0.10.0` and later carry the
  typed setting keys (`settings.keys`) into the bundle, and `v0.12.0` the `migrate:` step and the sets each item's
  image hosts; the runner needs Wheelhouse v0.5 or later.
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
  `ClientSecret`, `Scope`, …); none polls nothing. The image answers `dotnet Events.Api.dll intake check [--source <name>]
  [--foreign-subscription <id>]` from configuration only (no database): per enabled source, its own token pulls its
  subscription (`check=pull`, 200, with `records` and `event-records` of `wow2.event.v1`), is refused the tenants list
  (`check=tenants`, 403) and, with `--foreign-subscription`, finds no other tenant's subscription (`check=foreign`, 404,
  never 403). One `intake-check source=… check=… status=… expected=… ok=…` line per check, `reason=not-configured` for a
  source missing its base URL, subscription or token client, and a last `intake-check-result ok=…`; exit 0 when every
  check matches, else 1. It prints no token, header, body, URL or host name.
- Dev check: after a dev rollout, `verify` curls `$VERIFY_SERVICE_EVENTS/health` and `/api/system/status` with
  `Host: localhost`; `HostFilteringTests` replays the descriptor's requests.
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
