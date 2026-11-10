-- 004-post-catalog — a post's identity is per catalog (cross-lane review, 2026-11-10): two sources on one SCFD instance
-- feeding two organizations' catalogs keep one event each, never one shared. Widens the key; every existing row keeps it.
-- @contract since=v0.1

ALTER TABLE {{schema}}.events_source_posts DROP CONSTRAINT IF EXISTS pk_events_source_posts;
ALTER TABLE {{schema}}.events_source_posts ADD CONSTRAINT pk_events_source_posts PRIMARY KEY (source_instance, catalog_id, post_id);
