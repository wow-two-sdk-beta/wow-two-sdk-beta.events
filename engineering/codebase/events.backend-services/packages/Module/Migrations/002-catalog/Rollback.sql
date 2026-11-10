-- Inverts 002-catalog — drops every Events table. Dev/test recovery only: catalogs, tracking and evidence are lost.

DROP TABLE IF EXISTS {{schema}}.events_import_map;
DROP TABLE IF EXISTS {{schema}}.events_tracking_history;
DROP TABLE IF EXISTS {{schema}}.events_tracking;
DROP TABLE IF EXISTS {{schema}}.events_workflows;
DROP TABLE IF EXISTS {{schema}}.events_intake_cursors;
DROP TABLE IF EXISTS {{schema}}.events_source_posts;
DROP TABLE IF EXISTS {{schema}}.events_observations;
DROP TABLE IF EXISTS {{schema}}.events_event_sources;
DROP TABLE IF EXISTS {{schema}}.events_event_deadlines;
DROP TABLE IF EXISTS {{schema}}.events_catalog_events;
DROP TABLE IF EXISTS {{schema}}.events_catalogs;
