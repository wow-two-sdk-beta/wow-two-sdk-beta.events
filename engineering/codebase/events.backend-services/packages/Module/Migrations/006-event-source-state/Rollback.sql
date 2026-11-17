-- Inverts 006-event-source-state — drops each event source's name, listing state and posted time. Dev/test recovery only.

ALTER TABLE {{schema}}.events_event_sources DROP COLUMN IF EXISTS posted_at;
ALTER TABLE {{schema}}.events_event_sources DROP COLUMN IF EXISTS state;
ALTER TABLE {{schema}}.events_event_sources DROP COLUMN IF EXISTS name;
