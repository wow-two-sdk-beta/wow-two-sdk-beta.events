-- 006-event-source-state — where a sourced event's listing stands (events-integration E15): the source's name as people
-- know it, the listing's state at the source (`live`, `suspect`, `gone`, `unmatched`) and when the source posted it, so
-- a consumer shows "via Afisha" and "listing removed". Expands only: nullable columns, filled by each SCFD record from now
-- on; an import's or a curator's source keeps none.

ALTER TABLE {{schema}}.events_event_sources ADD COLUMN IF NOT EXISTS name varchar(200) NULL;
ALTER TABLE {{schema}}.events_event_sources ADD COLUMN IF NOT EXISTS state varchar(16) NULL;
ALTER TABLE {{schema}}.events_event_sources ADD COLUMN IF NOT EXISTS posted_at timestamptz NULL;
