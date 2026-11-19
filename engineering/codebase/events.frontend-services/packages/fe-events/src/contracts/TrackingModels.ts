import type { JsonValue, LocalizedText } from './Common';
import type { DeadlineKind, EventDeadline } from './EventFacts';
import type { EventModel } from './EventModel';

/** Mirrors `TrackingPriority`. */
export const TrackingPriorityValues = ['high', 'normal', 'low'] as const;
export type TrackingPriority = (typeof TrackingPriorityValues)[number];

/** Mirrors `TrackingReference`: a product entity the tracking points at, by opaque type and id. */
export interface TrackingReference {
  readonly type: string;
  readonly id: string;
}

/** Mirrors `TrackingModel`: one organization's private record about one event. */
export interface TrackingModel {
  readonly id: string;
  readonly eventId: string;
  readonly stage: string;
  readonly priority: TrackingPriority;
  readonly assignee?: string | null;
  readonly notes?: string;
  readonly fields?: JsonValue;
  readonly references?: readonly TrackingReference[];
  readonly revision: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

/** Mirrors `TrackedEventModel`: an event with the caller's organization's tracking, when it has any. */
export interface TrackedEventModel {
  readonly event: EventModel;
  readonly tracking?: TrackingModel | null;
}

/** Mirrors `TrackingPage`. */
export interface TrackingPage {
  readonly items: readonly TrackedEventModel[];
  readonly nextCursor?: string | null;
}

/**
 * Mirrors `TrackingQuery`; dates are `YYYY-MM-DD`. An untracked event reads as the workflow's default stage for `stage`,
 * `stages` and `excludeStages`; an event with no country matches a country filter only with `includeUnknownCountry`.
 */
export interface TrackingQuery {
  readonly trackedOnly?: boolean;
  readonly stage?: string;
  /** Stages to list, with `stage`. */
  readonly stages?: readonly string[];
  /** Stages to leave out; never one `stages` names. */
  readonly excludeStages?: readonly string[];
  readonly priority?: TrackingPriority;
  readonly from?: string;
  readonly to?: string;
  readonly kind?: string;
  readonly country?: string;
  /** ISO-2 countries to list, with `country`. */
  readonly countries?: readonly string[];
  /** ISO-2 countries to leave out. */
  readonly excludeCountries?: readonly string[];
  /** Whether an event with no country matches a country filter; false by default. */
  readonly includeUnknownCountry?: boolean;
  readonly text?: string;
  readonly deadlineBefore?: string;
  readonly includeUndated?: boolean;
  readonly cursor?: string;
  readonly limit?: number;
}

/** Mirrors `TrackingSpec`; the stage defaults to the workflow's default stage. */
export interface TrackingSpec {
  readonly stage?: string | null;
  readonly priority?: TrackingPriority;
  readonly assignee?: string | null;
  readonly notes?: string;
  readonly fields?: JsonValue;
  readonly references?: readonly TrackingReference[];
  readonly expectedRevision?: number | null;
}

/** Mirrors `TrackingHistoryEntryModel`: one stage change. */
export interface TrackingHistoryEntryModel {
  readonly fromStage?: string | null;
  readonly toStage: string;
  readonly actorIssuer?: string | null;
  readonly actorSubject?: string | null;
  readonly atUtc: string;
}

/** Mirrors `UpcomingDeadlineModel`. */
export interface UpcomingDeadlineModel {
  readonly eventId: string;
  readonly title: LocalizedText;
  readonly deadline: EventDeadline;
  /** The tracking's stage; the workflow's default stage for an untracked event. */
  readonly stage: string;
  /** Whether the organization tracks the event; absent (tracked) from a service before untracked deadlines. */
  readonly tracked?: boolean;
}

/**
 * Mirrors `TrackingSummaryModel`: tracking counted per stage, the published events not tracked, and the nearest open
 * deadlines of events that are not cancelled, untracked ones included.
 */
export interface TrackingSummaryModel {
  readonly byStage: Readonly<Record<string, number>>;
  /** Published events the organization does not track; absent from a service before E21. */
  readonly untracked?: number;
  readonly upcomingDeadlines: readonly UpcomingDeadlineModel[];
}

/** Mirrors `TrackingSummaryQuery`: the day deadlines count from (`YYYY-MM-DD`) and the deadline kinds to list. */
export interface TrackingSummaryQuery {
  readonly today: string;
  /** Every kind when absent or empty. */
  readonly deadlineKinds?: readonly DeadlineKind[];
}
