-- 002-catalog — catalogs, their events, the observations sources deliver, intake cursors, workflows, tracking and the
-- import id map (events-integration #14, #19, #26).
--
-- Enumerations are text with check constraints whose labels are the contract's members in camelCase, as on the wire.
-- Structured facts live as jsonb (`facts`, `links`, …); the columns beside them are what lists filter and sort on, kept
-- by the module on every write. Organizations and people are keyed by issuer and id, never a local copy.

CREATE TABLE IF NOT EXISTS events_catalogs (
    id                    uuid         NOT NULL,
    slug                  varchar(64)  NOT NULL,
    name                  varchar(200) NOT NULL,
    owner_issuer          varchar(512) NOT NULL,
    owner_organization_id varchar(128) NOT NULL,
    visibility            varchar(16)  NOT NULL,
    locales               jsonb        NOT NULL,
    kinds                 jsonb        NOT NULL,
    revision              integer      NOT NULL,
    created_at            timestamptz  NOT NULL,
    updated_at            timestamptz  NOT NULL,
    CONSTRAINT pk_events_catalogs PRIMARY KEY (id),
    CONSTRAINT ck_events_catalogs_visibility CHECK (visibility IN ('private', 'shared', 'public')),
    CONSTRAINT ck_events_catalogs_revision CHECK (revision > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_events_catalogs_slug ON events_catalogs (slug);

-- One row per event. `title_sort` and `search` are lowercased; `starts_min`/`starts_max` and `ends_max` are the date
-- span a value covers at its precision (a month covers its whole month), null when unknown.
CREATE TABLE IF NOT EXISTS events_catalog_events (
    id           uuid         NOT NULL,
    catalog_id   uuid         NOT NULL,
    key          varchar(64)  NULL,
    review       varchar(16)  NOT NULL,
    merged_into  uuid         NULL,
    facts        jsonb        NOT NULL,
    title_sort   text         NOT NULL,
    search       text         NOT NULL,
    kind         varchar(64)  NOT NULL,
    status       varchar(16)  NOT NULL,
    country      varchar(2)   NULL,
    city         text         NULL,
    starts_min   date         NULL,
    starts_max   date         NULL,
    ends_max     date         NULL,
    revision     integer      NOT NULL,
    created_at   timestamptz  NOT NULL,
    updated_at   timestamptz  NOT NULL,
    published_at timestamptz  NULL,
    CONSTRAINT pk_events_catalog_events PRIMARY KEY (id),
    CONSTRAINT fk_events_catalog_events_catalog FOREIGN KEY (catalog_id) REFERENCES events_catalogs (id),
    CONSTRAINT ck_events_catalog_events_review CHECK (review IN ('candidate', 'published', 'rejected', 'archived')),
    CONSTRAINT ck_events_catalog_events_status CHECK (status IN ('scheduled', 'postponed', 'rescheduled', 'cancelled', 'movedOnline')),
    CONSTRAINT ck_events_catalog_events_revision CHECK (revision > 0),
    CONSTRAINT ck_events_catalog_events_span CHECK (starts_min IS NULL OR starts_max >= starts_min)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_events_catalog_events_key ON events_catalog_events (catalog_id, key) WHERE key IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_events_catalog_events_list ON events_catalog_events (catalog_id, review, starts_min NULLS LAST, title_sort, id);

-- Each deadline's span, for the deadline filter and the tracking summary.
CREATE TABLE IF NOT EXISTS events_event_deadlines (
    event_id uuid        NOT NULL,
    ordinal  integer     NOT NULL,
    kind     varchar(16) NOT NULL,
    at_min   date        NULL,
    at_max   date        NULL,
    CONSTRAINT pk_events_event_deadlines PRIMARY KEY (event_id, ordinal),
    CONSTRAINT fk_events_event_deadlines_event FOREIGN KEY (event_id) REFERENCES events_catalog_events (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_events_event_deadlines_at ON events_event_deadlines (at_min) WHERE at_min IS NOT NULL;

-- Every source identity that reported an event; one identity maps to one event per catalog.
CREATE TABLE IF NOT EXISTS events_event_sources (
    catalog_id    uuid         NOT NULL,
    kind          varchar(16)  NOT NULL,
    source_id     varchar(256) NOT NULL,
    event_id      uuid         NOT NULL,
    url           text         NULL,
    first_seen_at timestamptz  NOT NULL,
    last_seen_at  timestamptz  NOT NULL,
    CONSTRAINT pk_events_event_sources PRIMARY KEY (catalog_id, kind, source_id),
    CONSTRAINT fk_events_event_sources_event FOREIGN KEY (event_id) REFERENCES events_catalog_events (id) ON DELETE CASCADE,
    CONSTRAINT ck_events_event_sources_kind CHECK (kind IN ('scfd', 'import', 'manual'))
);

CREATE INDEX IF NOT EXISTS ix_events_event_sources_event ON events_event_sources (event_id);

-- One row per delivered record, kept as evidence whatever intake did with it. `seq` orders pages newest first.
CREATE TABLE IF NOT EXISTS events_observations (
    id                  uuid         NOT NULL,
    seq                 bigint       GENERATED ALWAYS AS IDENTITY,
    catalog_id          uuid         NOT NULL,
    source_kind         varchar(16)  NOT NULL,
    source_instance     varchar(128) NULL,
    subscription_id     varchar(64)  NULL,
    pipeline_id         varchar(64)  NULL,
    delivery_event_id   uuid         NULL,
    item_id             varchar(128) NULL,
    type                varchar(64)  NOT NULL,
    reason              varchar(32)  NULL,
    post_id             uuid         NULL,
    revision            integer      NULL,
    observed_at         timestamptz  NOT NULL,
    received_at         timestamptz  NOT NULL,
    mapping_version     varchar(64)  NULL,
    links               jsonb        NOT NULL,
    payload             jsonb        NULL,
    proposed            jsonb        NULL,
    state               varchar(16)  NOT NULL,
    quarantine_reason   text         NULL,
    event_id            uuid         NULL,
    resolved_at         timestamptz  NULL,
    resolved_by_issuer  varchar(512) NULL,
    resolved_by_subject varchar(256) NULL,
    CONSTRAINT pk_events_observations PRIMARY KEY (id),
    CONSTRAINT fk_events_observations_catalog FOREIGN KEY (catalog_id) REFERENCES events_catalogs (id),
    CONSTRAINT ck_events_observations_kind CHECK (source_kind IN ('scfd', 'import', 'manual')),
    CONSTRAINT ck_events_observations_state CHECK (state IN ('applied', 'pending', 'superseded', 'quarantined', 'recorded', 'accepted', 'dismissed', 'ignored'))
);

-- A retry of the same delivery is a no-op: dedup on the instance, subscription and delivery id.
CREATE UNIQUE INDEX IF NOT EXISTS ux_events_observations_delivery
    ON events_observations (source_instance, subscription_id, delivery_event_id) WHERE delivery_event_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_events_observations_catalog ON events_observations (catalog_id, state, seq);
CREATE INDEX IF NOT EXISTS ix_events_observations_event ON events_observations (event_id) WHERE event_id IS NOT NULL;

-- The newest revision intake has applied per post, so an older revision arriving late is superseded. A post is one
-- SCFD instance's: two subscriptions delivering the same post feed one event, and its revisions order across both;
-- `subscription_id` records the last subscription that delivered it.
CREATE TABLE IF NOT EXISTS events_source_posts (
    source_instance       varchar(128) NOT NULL,
    post_id               uuid         NOT NULL,
    subscription_id       varchar(64)  NOT NULL,
    catalog_id            uuid         NOT NULL,
    event_id              uuid         NULL,
    latest_revision       integer      NOT NULL,
    latest_observation_id uuid         NOT NULL,
    updated_at            timestamptz  NOT NULL,
    CONSTRAINT pk_events_source_posts PRIMARY KEY (source_instance, post_id),
    CONSTRAINT fk_events_source_posts_catalog FOREIGN KEY (catalog_id) REFERENCES events_catalogs (id)
);

-- One cursor per configured intake source, committed in the same transaction as its page's observations.
CREATE TABLE IF NOT EXISTS events_intake_cursors (
    source_name          varchar(64)  NOT NULL,
    subscription_id      varchar(64)  NOT NULL,
    cursor               text         NULL,
    last_polled_at       timestamptz  NULL,
    last_received_at     timestamptz  NULL,
    newest_observed_at   timestamptz  NULL,
    last_error           text         NULL,
    consecutive_failures integer      NOT NULL,
    received             bigint       NOT NULL,
    quarantined          bigint       NOT NULL,
    updated_at           timestamptz  NOT NULL,
    CONSTRAINT pk_events_intake_cursors PRIMARY KEY (source_name)
);

CREATE TABLE IF NOT EXISTS events_workflows (
    issuer          varchar(512) NOT NULL,
    organization_id varchar(128) NOT NULL,
    stages          jsonb        NOT NULL,
    default_stage   varchar(64)  NOT NULL,
    fields          jsonb        NULL,
    revision        integer      NOT NULL,
    updated_at      timestamptz  NOT NULL,
    CONSTRAINT pk_events_workflows PRIMARY KEY (issuer, organization_id),
    CONSTRAINT ck_events_workflows_revision CHECK (revision > 0)
);

CREATE TABLE IF NOT EXISTS events_tracking (
    id              uuid         NOT NULL,
    issuer          varchar(512) NOT NULL,
    organization_id varchar(128) NOT NULL,
    event_id        uuid         NOT NULL,
    stage           varchar(64)  NOT NULL,
    priority        varchar(8)   NOT NULL,
    assignee        varchar(256) NULL,
    notes           text         NOT NULL,
    fields          jsonb        NULL,
    refs            jsonb        NOT NULL,
    revision        integer      NOT NULL,
    created_at      timestamptz  NOT NULL,
    updated_at      timestamptz  NOT NULL,
    CONSTRAINT pk_events_tracking PRIMARY KEY (id),
    CONSTRAINT fk_events_tracking_event FOREIGN KEY (event_id) REFERENCES events_catalog_events (id),
    CONSTRAINT ck_events_tracking_priority CHECK (priority IN ('high', 'normal', 'low')),
    CONSTRAINT ck_events_tracking_notes CHECK (char_length(notes) <= 8000),
    CONSTRAINT ck_events_tracking_revision CHECK (revision > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_events_tracking_event ON events_tracking (issuer, organization_id, event_id);

CREATE TABLE IF NOT EXISTS events_tracking_history (
    id            uuid         NOT NULL,
    tracking_id   uuid         NOT NULL,
    from_stage    varchar(64)  NULL,
    to_stage      varchar(64)  NOT NULL,
    actor_issuer  varchar(512) NULL,
    actor_subject varchar(256) NULL,
    at            timestamptz  NOT NULL,
    CONSTRAINT pk_events_tracking_history PRIMARY KEY (id),
    CONSTRAINT fk_events_tracking_history_tracking FOREIGN KEY (tracking_id) REFERENCES events_tracking (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_events_tracking_history_tracking ON events_tracking_history (tracking_id, at);

-- The stable old → new id map of imports: a rerun finds each row here.
CREATE TABLE IF NOT EXISTS events_import_map (
    catalog_id      uuid         NOT NULL,
    issuer          varchar(512) NOT NULL,
    organization_id varchar(128) NOT NULL,
    legacy_id       varchar(256) NOT NULL,
    source          varchar(128) NOT NULL,
    event_id        uuid         NOT NULL,
    tracking_id     uuid         NULL,
    imported_at     timestamptz  NOT NULL,
    updated_at      timestamptz  NOT NULL,
    CONSTRAINT pk_events_import_map PRIMARY KEY (catalog_id, issuer, organization_id, legacy_id),
    CONSTRAINT fk_events_import_map_catalog FOREIGN KEY (catalog_id) REFERENCES events_catalogs (id),
    CONSTRAINT fk_events_import_map_event FOREIGN KEY (event_id) REFERENCES events_catalog_events (id)
);
