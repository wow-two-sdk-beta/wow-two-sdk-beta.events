import type { JsonValue } from './Common';
import type { EventFacts } from './EventFacts';
import type { TrackingPriority, TrackingReference } from './TrackingModels';

/** Mirrors `EventImportOutcome`. */
export const EventImportOutcomeValues = ['created', 'updated', 'unchanged', 'failed'] as const;
export type EventImportOutcome = (typeof EventImportOutcomeValues)[number];

/** Mirrors `EventImportTracking`: the importing organization's tracking of one row. */
export interface EventImportTracking {
  readonly stage: string;
  readonly priority?: TrackingPriority;
  readonly notes?: string;
  readonly fields?: JsonValue;
  readonly references?: readonly TrackingReference[];
}

/** Mirrors `EventImportRow`: one product event, keyed by its old id so a rerun maps to the same new ids. */
export interface EventImportRow {
  readonly legacyId: string;
  readonly facts: EventFacts;
  readonly sourceUrl?: string | null;
  readonly recordedAtUtc?: string | null;
  readonly tracking?: EventImportTracking | null;
  /** The product's own row as it was, kept as evidence. */
  readonly legacy?: JsonValue;
}

/** Mirrors `EventImportBundle` (`wow2.events-import.v1`). */
export interface EventImportBundle {
  readonly format?: string;
  readonly source: string;
  readonly rows: readonly EventImportRow[];
}

/** Mirrors `EventImportMapEntry`: one row's old id → new ids. */
export interface EventImportMapEntry {
  readonly legacyId: string;
  readonly eventId: string;
  readonly trackingId?: string | null;
  readonly outcome: EventImportOutcome;
  readonly error?: string | null;
}

/** Mirrors `EventImportReport`. */
export interface EventImportReport {
  readonly dryRun: boolean;
  readonly source: string;
  readonly created: number;
  readonly updated: number;
  readonly unchanged: number;
  readonly failed: number;
  readonly map: readonly EventImportMapEntry[];
}
