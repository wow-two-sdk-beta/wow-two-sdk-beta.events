import type { EventFacts, EventStatus } from './EventFacts';

/** Mirrors `EventReviewState`; consumers see `published`. */
export const EventReviewStateValues = ['candidate', 'published', 'rejected', 'archived'] as const;
export type EventReviewState = (typeof EventReviewStateValues)[number];

/** Mirrors `EventSourceKind`. */
export const EventSourceKindValues = ['scfd', 'import', 'manual'] as const;
export type EventSourceKind = (typeof EventSourceKindValues)[number];

/** Mirrors `EventSourceState`: where a source's listing stands, as the source last reported it. */
export const EventSourceStateValues = ['live', 'suspect', 'gone', 'unmatched'] as const;
export type EventSourceState = (typeof EventSourceStateValues)[number];

/** Mirrors `EventSourceModel`: one source identity that reported an event. */
export interface EventSourceModel {
  readonly kind: EventSourceKind;
  readonly id: string;
  readonly url?: string | null;
  /** The source's name as people know it, such as `Afisha`; none for an import or a curator's entry. */
  readonly name?: string | null;
  /** Where the source's listing stands; none when the source reports no state. */
  readonly state?: EventSourceState | null;
  /** When the source posted the listing (UTC); none when it does not say. */
  readonly postedAtUtc?: string | null;
  /**
   * The rule that attached the source to this event after another source started it, such as `rule:title-date-city`;
   * none for the source the event started from, an import's and a curator's.
   */
  readonly matchedBy?: string | null;
  readonly firstSeenAtUtc: string;
  readonly lastSeenAtUtc: string;
}

/** Mirrors `EventModel`: a catalog's event, as reviewed. */
export interface EventModel {
  readonly id: string;
  readonly catalog: string;
  readonly facts: EventFacts;
  readonly review: EventReviewState;
  readonly mergedInto?: string | null;
  readonly sources?: readonly EventSourceModel[];
  readonly pendingChanges?: number;
  readonly revision: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
  readonly publishedAtUtc?: string | null;
  /** Who decided the review: `person:{subject}` or `classifier:{name}`. */
  readonly reviewedBy?: string | null;
  /** Why: a filter that passed it, or the reason a classifier held it. */
  readonly reviewReason?: string | null;
  readonly reviewedAtUtc?: string | null;
}

/** Mirrors `EventPage`. */
export interface EventPage {
  readonly items: readonly EventModel[];
  readonly nextCursor?: string | null;
}

/** Mirrors `EventSpec`: the facts a curator writes or a member proposes. */
export interface EventSpec {
  readonly facts: EventFacts;
  readonly expectedRevision?: number | null;
}

/** Mirrors `EventQuery`; dates are `YYYY-MM-DD`, a month value covers its whole month. */
export interface EventQuery {
  readonly from?: string;
  readonly to?: string;
  readonly kind?: string;
  readonly country?: string;
  readonly city?: string;
  readonly text?: string;
  readonly status?: EventStatus;
  readonly review?: EventReviewState;
  readonly deadlineBefore?: string;
  readonly includeUndated?: boolean;
  readonly cursor?: string;
  readonly limit?: number;
}
