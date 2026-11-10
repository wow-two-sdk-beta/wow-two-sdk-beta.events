-- Inverts 002-catalog — drops every Events table. Dev/test recovery only: catalogs, tracking and evidence are lost.

DROP TABLE IF EXISTS events_import_map;
DROP TABLE IF EXISTS events_tracking_history;
DROP TABLE IF EXISTS events_tracking;
DROP TABLE IF EXISTS events_workflows;
DROP TABLE IF EXISTS events_intake_cursors;
DROP TABLE IF EXISTS events_source_posts;
DROP TABLE IF EXISTS events_observations;
DROP TABLE IF EXISTS events_event_sources;
DROP TABLE IF EXISTS events_event_deadlines;
DROP TABLE IF EXISTS events_catalog_events;
DROP TABLE IF EXISTS events_catalogs;
