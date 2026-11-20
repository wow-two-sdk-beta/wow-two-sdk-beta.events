import type { JsonValue, LocalizedText } from './Common';
import type { DeadlineKind, EventDeadline, EventStatus } from './EventFacts';
import type { EventModel } from './EventModel';

/** Mirrors `TrackingPriority`. */
export const TrackingPriorityValues = ['high', 'normal', 'low'] as const;
export type TrackingPriority = (typeof TrackingPriorityValues)[number];

/** Mirrors `TrackingSort`: how a tracking list is ordered; every order ends on the event id. */
export const TrackingSortValues = ['starts', 'added', 'deadline'] as const;
export type TrackingSort = (typeof TrackingSortValues)[number];

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
 * A list filter matches an event in any of its values, and the single value beside it (`kind`, `city`) adds to the list.
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
  /** Kinds to list, with `kind`; at most 50. */
  readonly kinds?: readonly string[];
  /** The city to list, matched without case. */
  readonly city?: string;
  /** Cities to list, with `city`, matched without case; at most 50. */
  readonly cities?: readonly string[];
  /** The event status to list. */
  readonly status?: EventStatus;
  /**
   * Source names to list, matched without case against the name each source of an event is stored under (`Afisha`): an
   * event lists when any of its sources is one of them. The reserved `curated` (`EventsConstants.curatedSource`) lists
   * the events no intake source reported: imports and curators' own entries. At most 50.
   */
  readonly sources?: readonly string[];
  /** The first instant an event may have been published at (`2026-11-10T09:00:00Z`): lists events published on or after it. */
  readonly publishedSince?: string;
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
  /** `starts` when absent: start ascending, unknown last. `added` is newest published first; `deadline` the nearest open deadline first, none last. */
  readonly sort?: TrackingSort;
  /** The cursor a page returned; it holds for the sort it came from, and another sort answers a validation failure. */
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

/**
 * Mirrors `TrackingFacetCount`: how many events one facet value matches. `value` is what the list's filter takes back; only
 * `sources` carries a `label`, the source's name as stored (`Afisha`).
 */
export interface TrackingFacetCount {
  readonly value: string;
  readonly count: number;
  readonly label?: string | null;
}

/** Mirrors `TrackingDeadlineFacet`: the events with an open deadline starting on or before `before` (`YYYY-MM-DD`). */
export interface TrackingDeadlineFacet {
  /** The query's `deadlineBefore`, else 30 days after today (UTC). */
  readonly before: string;
  readonly count: number;
}

/**
 * Mirrors `TrackingFacetsModel`: the counts behind a tracking list's filter chips for one query. Each facet is counted
 * without its own selection (and under every other), so a chip shows what picking it would list; `total` counts the whole
 * query.
 */
export interface TrackingFacetsModel {
  readonly total: number;
  /** ISO-2 country counts, most first; events with no country count nowhere. */
  readonly countries: readonly TrackingFacetCount[];
  /** Lowercase city counts, most first. */
  readonly cities: readonly TrackingFacetCount[];
  /** The catalog's kinds in its order, zeros included, then any other kind found. */
  readonly kinds: readonly TrackingFacetCount[];
  /** The workflow's stages in order, zeros included; an untracked event counts under the default stage. */
  readonly stages: readonly TrackingFacetCount[];
  /** `high`, `normal` and `low`, zeros included; tracked events only. */
  readonly priorities: readonly TrackingFacetCount[];
  /** Lowercase source-name counts with their labels, most first, plus `curated`. */
  readonly sources: readonly TrackingFacetCount[];
  readonly openDeadline: TrackingDeadlineFacet;
}
