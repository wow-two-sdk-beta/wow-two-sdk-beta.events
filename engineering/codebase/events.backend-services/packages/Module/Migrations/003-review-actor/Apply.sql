-- 003-review-actor — who reviewed an event and why (events-integration #53): a person (`person:{subject}`) or an intake
-- source's classifier (`classifier:{name}`), with its verdict as the reason, so an automatic accept is explained and a
-- reviewer can reopen it. Expands only: nullable columns, set by every review from now on.

ALTER TABLE {{schema}}.events_catalog_events ADD COLUMN IF NOT EXISTS reviewed_by varchar(256) NULL;
ALTER TABLE {{schema}}.events_catalog_events ADD COLUMN IF NOT EXISTS review_reason text NULL;
ALTER TABLE {{schema}}.events_catalog_events ADD COLUMN IF NOT EXISTS reviewed_at timestamptz NULL;
