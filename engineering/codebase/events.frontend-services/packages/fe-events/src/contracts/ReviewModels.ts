import type { JsonValue } from './Common';
import type { EventFacts } from './EventFacts';
import type { EventModel, EventSourceKind } from './EventModel';

/** Mirrors `ObservationState`: what intake did with an observation. */
export const ObservationStateValues = [
  'applied',
  'pending',
  'superseded',
  'quarantined',
  'recorded',
  'accepted',
  'dismissed',
  'ignored',
] as const;
export type ObservationState = (typeof ObservationStateValues)[number];

/** Mirrors `ObservationLinkModel`: one source link behind an observation — where it was read and what was captured. */
export interface ObservationLinkModel {
  readonly url?: string | null;
  readonly sourceId?: string | null;
  readonly captureId?: string | null;
  readonly capturedAtUtc?: string | null;
  readonly sourceFields?: JsonValue;
}

/** Mirrors `ObservationModel`: one record a source delivered, with its evidence and what intake did with it. */
export interface ObservationModel {
  readonly id: string;
  readonly catalog: string;
  readonly sourceKind: EventSourceKind;
  readonly sourceInstance?: string | null;
  readonly subscriptionId?: string | null;
  readonly pipelineId?: string | null;
  readonly deliveryEventId?: string | null;
  readonly itemId?: string | null;
  readonly type: string;
  readonly reason?: string | null;
  readonly postId?: string | null;
  readonly revision?: number | null;
  readonly observedAtUtc: string;
  readonly receivedAtUtc: string;
  readonly mappingVersion?: string | null;
  readonly links?: readonly ObservationLinkModel[];
  readonly state: ObservationState;
  readonly quarantineReason?: string | null;
  readonly eventId?: string | null;
  readonly proposed?: EventFacts | null;
  readonly payload?: JsonValue;
}

/** Mirrors `ObservationPage`. */
export interface ObservationPage {
  readonly items: readonly ObservationModel[];
  readonly nextCursor?: string | null;
}

/** Mirrors `ObservationQuery`. */
export interface ObservationQuery {
  readonly state?: ObservationState;
  readonly eventId?: string;
  readonly cursor?: string;
  readonly limit?: number;
}

/**
 * Mirrors `ReviewItemModel`: a candidate, or a published event with source changes waiting. Its observations are the
 * pending changes, oldest first; a candidate's own observations are its evidence.
 */
export interface ReviewItemModel {
  readonly event: EventModel;
  readonly observations?: readonly ObservationModel[];
}

/** Mirrors `ReviewPage`. */
export interface ReviewPage {
  readonly items: readonly ReviewItemModel[];
  readonly nextCursor?: string | null;
}

/** Mirrors `ReviewQuery`. */
export interface ReviewQuery {
  readonly cursor?: string;
  readonly limit?: number;
}

/** Mirrors `ReviewDecision`: accepting or applying, with the reviewer's edits when `facts` is given. */
export interface ReviewDecision {
  readonly expectedRevision: number;
  readonly facts?: EventFacts | null;
}

/** Mirrors `MergeDecision`: merging a candidate into an existing event. */
export interface MergeDecision {
  readonly intoId: string;
  readonly expectedRevision: number;
  readonly intoExpectedRevision: number;
}
