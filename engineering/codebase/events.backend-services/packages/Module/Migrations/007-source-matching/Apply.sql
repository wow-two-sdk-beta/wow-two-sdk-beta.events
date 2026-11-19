-- 007-source-matching — one event behind several sources (events-integration E17): a source that joined an event by
-- rule records how (`matched_by`, e.g. `rule:title-date-city`; null for the source an event started from), and a
-- reviewer's "split source" records the pair it kept apart, so intake never attaches that source to that event again.
-- Expands only: a nullable column and a new table.

ALTER TABLE {{schema}}.events_event_sources ADD COLUMN IF NOT EXISTS matched_by varchar(64) NULL;

-- One row per source identity a reviewer split off an event: (event, source) never match again. The source's own new
-- candidate is not in it; the event it left is.
CREATE TABLE IF NOT EXISTS {{schema}}.events_event_keep_apart (
    catalog_id  uuid         NOT NULL,
    event_id    uuid         NOT NULL,
    source_kind varchar(16)  NOT NULL,
    source_id   varchar(256) NOT NULL,
    created_at  timestamptz  NOT NULL,
    created_by  varchar(256) NULL,
    CONSTRAINT pk_events_event_keep_apart PRIMARY KEY (catalog_id, source_kind, source_id, event_id),
    CONSTRAINT fk_events_event_keep_apart_event FOREIGN KEY (event_id) REFERENCES {{schema}}.events_catalog_events (id) ON DELETE CASCADE,
    CONSTRAINT ck_events_event_keep_apart_kind CHECK (source_kind IN ('scfd', 'import', 'manual'))
);

CREATE INDEX IF NOT EXISTS ix_events_event_keep_apart_event ON {{schema}}.events_event_keep_apart (event_id);
