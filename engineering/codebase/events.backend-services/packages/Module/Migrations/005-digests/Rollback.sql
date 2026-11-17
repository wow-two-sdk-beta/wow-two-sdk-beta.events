-- Inverts 005-digests — drops the digests, their runs and their deliveries. Dev/test recovery only: definitions and run
-- history are lost.

DROP TABLE IF EXISTS {{schema}}.events_digest_deliveries;
DROP TABLE IF EXISTS {{schema}}.events_digest_runs;
DROP TABLE IF EXISTS {{schema}}.events_digests;
