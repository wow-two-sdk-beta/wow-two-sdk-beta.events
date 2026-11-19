-- Inverts 007-source-matching — drops the keep-apart pairs and each source's match rule; an event that gathered several
-- sources keeps them. Dev/test recovery only.

DROP TABLE IF EXISTS {{schema}}.events_event_keep_apart;
ALTER TABLE {{schema}}.events_event_sources DROP COLUMN IF EXISTS matched_by;
