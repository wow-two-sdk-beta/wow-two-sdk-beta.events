-- Inverts 004-post-catalog — a post's identity per SCFD instance again. Fails while one post feeds two catalogs.
-- Dev/test recovery only.

ALTER TABLE {{schema}}.events_source_posts DROP CONSTRAINT IF EXISTS pk_events_source_posts;
ALTER TABLE {{schema}}.events_source_posts ADD CONSTRAINT pk_events_source_posts PRIMARY KEY (source_instance, post_id);
