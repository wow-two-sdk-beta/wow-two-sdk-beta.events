-- Inverts 003-review-actor — drops who reviewed each event and why. Dev/test recovery only.

ALTER TABLE {{schema}}.events_catalog_events DROP COLUMN IF EXISTS reviewed_at;
ALTER TABLE {{schema}}.events_catalog_events DROP COLUMN IF EXISTS review_reason;
ALTER TABLE {{schema}}.events_catalog_events DROP COLUMN IF EXISTS reviewed_by;
