-- 005-digests — scheduled event digests (V07): an organization's digests of a catalog, each run of one, and what each
-- recipient got. A run is unique per digest and occurrence, so an occurrence goes out once, across restarts and hosts.
-- Expands only: three new tables.

CREATE TABLE IF NOT EXISTS {{schema}}.events_digests (
    id              uuid         NOT NULL,
    catalog_id      uuid         NOT NULL,
    issuer          varchar(512) NOT NULL,
    organization_id varchar(128) NOT NULL,
    name            varchar(200) NOT NULL,
    enabled         boolean      NOT NULL,
    channel         varchar(32)  NOT NULL,
    time_zone       varchar(64)  NOT NULL,
    schedule        jsonb        NOT NULL,
    selection       jsonb        NOT NULL,
    recipients      jsonb        NOT NULL,
    language        varchar(16)  NOT NULL,
    item_cap        integer      NOT NULL,
    link_template   text         NULL,
    schedule_since  timestamptz  NOT NULL,
    next_run_at     timestamptz  NULL,
    revision        integer      NOT NULL,
    created_at      timestamptz  NOT NULL,
    updated_at      timestamptz  NOT NULL,
    CONSTRAINT pk_events_digests PRIMARY KEY (id),
    CONSTRAINT fk_events_digests_catalog FOREIGN KEY (catalog_id) REFERENCES {{schema}}.events_catalogs (id),
    CONSTRAINT ck_events_digests_item_cap CHECK (item_cap BETWEEN 1 AND 50),
    CONSTRAINT ck_events_digests_revision CHECK (revision > 0)
);

CREATE INDEX IF NOT EXISTS ix_events_digests_owner ON {{schema}}.events_digests (catalog_id, issuer, organization_id, name);
CREATE INDEX IF NOT EXISTS ix_events_digests_due ON {{schema}}.events_digests (next_run_at) WHERE enabled AND next_run_at IS NOT NULL;

-- One row per occurrence a digest ran for (a send now's own instant). `event_ids` and `texts` keep what went out, so a
-- resumed run resends the same text under the same idempotency keys; `lease_until` keeps two hosts off one run.
CREATE TABLE IF NOT EXISTS {{schema}}.events_digest_runs (
    id            uuid        NOT NULL,
    digest_id     uuid        NOT NULL,
    occurrence_at timestamptz NOT NULL,
    trigger       varchar(16) NOT NULL,
    status        varchar(16) NOT NULL,
    matched       integer     NOT NULL,
    selected      integer     NOT NULL,
    event_ids     jsonb       NOT NULL,
    items         jsonb       NOT NULL,
    texts         jsonb       NOT NULL,
    notes         jsonb       NOT NULL,
    lease_until   timestamptz NULL,
    started_at    timestamptz NOT NULL,
    finished_at   timestamptz NULL,
    CONSTRAINT pk_events_digest_runs PRIMARY KEY (id),
    CONSTRAINT fk_events_digest_runs_digest FOREIGN KEY (digest_id) REFERENCES {{schema}}.events_digests (id) ON DELETE CASCADE,
    CONSTRAINT ck_events_digest_runs_trigger CHECK (trigger IN ('schedule', 'manual')),
    CONSTRAINT ck_events_digest_runs_status CHECK (status IN ('running', 'completed', 'partial', 'failed', 'empty', 'missed'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_events_digest_runs_occurrence ON {{schema}}.events_digest_runs (digest_id, occurrence_at);
CREATE INDEX IF NOT EXISTS ix_events_digest_runs_running ON {{schema}}.events_digest_runs (lease_until) WHERE status = 'running';

-- One row per recipient of a run; statuses are Identity's app notice words.
CREATE TABLE IF NOT EXISTS {{schema}}.events_digest_deliveries (
    run_id          uuid         NOT NULL,
    subject         varchar(256) NOT NULL,
    ordinal         integer      NOT NULL,
    language        varchar(16)  NOT NULL,
    status          varchar(16)  NOT NULL,
    error           text         NULL,
    attempts        integer      NOT NULL,
    next_attempt_at timestamptz  NULL,
    updated_at      timestamptz  NOT NULL,
    CONSTRAINT pk_events_digest_deliveries PRIMARY KEY (run_id, subject),
    CONSTRAINT fk_events_digest_deliveries_run FOREIGN KEY (run_id) REFERENCES {{schema}}.events_digest_runs (id) ON DELETE CASCADE,
    CONSTRAINT ck_events_digest_deliveries_status CHECK (status IN ('pending', 'sent', 'not_linked', 'muted', 'failed'))
);
