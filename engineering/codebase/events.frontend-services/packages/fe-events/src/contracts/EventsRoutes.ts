import type { DigestRunQuery } from './DigestModels';
import type { EventQuery } from './EventModel';
import type { ObservationQuery, ReviewQuery } from './ReviewModels';
import type { TrackingQuery, TrackingSummaryQuery } from './TrackingModels';

/** Mirrors `EventsRouteConstants` of the be contract: the HTTP mirror's segments, relative to the service root. */
export const EventsRouteConstants = {
  root: 'api/events/v1',
  catalogs: 'api/events/v1/catalogs',
  events: 'events',
  candidates: 'candidates',
  review: 'review',
  observations: 'observations',
  imports: 'imports',
  calendar: 'events.ics',
  tracking: 'tracking',
  digests: 'digests',
  freshness: 'freshness',
  workflow: 'api/events/v1/workflow',
  intake: 'api/events/v1/intake',
  signals: 'signals/identity',
} as const;

const segment = (value: string) => encodeURIComponent(value);
const catalog = (slug: string) => `${EventsRouteConstants.catalogs}/${segment(slug)}`;

/**
 * Builds the HTTP mirror's routes, relative to the service root (no leading slash): prefix them with the service URL
 * or the host app's proxy prefix. Slugs and ids are encoded.
 */
export const EventsRoutes = {
  catalogs: () => EventsRouteConstants.catalogs,
  catalog,
  events: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.events}`,
  event: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.events}/${segment(id)}`,
  candidates: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.candidates}`,
  calendar: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.calendar}`,
  review: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.review}`,
  accept: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.review}/${segment(id)}/accept`,
  reject: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.review}/${segment(id)}/reject`,
  reopen: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.review}/${segment(id)}/reopen`,
  merge: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.review}/${segment(id)}/merge`,
  splitSource: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.review}/${segment(id)}/split`,
  observations: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.observations}`,
  observation: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.observations}/${segment(id)}`,
  applyChange: (slug: string, id: string) =>
    `${catalog(slug)}/${EventsRouteConstants.observations}/${segment(id)}/apply`,
  dismissChange: (slug: string, id: string) =>
    `${catalog(slug)}/${EventsRouteConstants.observations}/${segment(id)}/dismiss`,
  tracking: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.tracking}`,
  trackingSummary: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.tracking}/summary`,
  trackingFacets: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.tracking}/facets`,
  trackedEvent: (slug: string, eventId: string) =>
    `${catalog(slug)}/${EventsRouteConstants.tracking}/${segment(eventId)}`,
  trackingHistory: (slug: string, eventId: string) =>
    `${catalog(slug)}/${EventsRouteConstants.tracking}/${segment(eventId)}/history`,
  imports: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.imports}`,
  digests: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.digests}`,
  digest: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.digests}/${segment(id)}`,
  digestPreview: (slug: string, id: string) =>
    `${catalog(slug)}/${EventsRouteConstants.digests}/${segment(id)}/preview`,
  digestSend: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.digests}/${segment(id)}/send`,
  digestRuns: (slug: string, id: string) => `${catalog(slug)}/${EventsRouteConstants.digests}/${segment(id)}/runs`,
  freshness: (slug: string) => `${catalog(slug)}/${EventsRouteConstants.freshness}`,
  workflow: () => EventsRouteConstants.workflow,
  intake: () => EventsRouteConstants.intake,
  intakePoll: () => `${EventsRouteConstants.intake}/poll`,
} as const;

/** Holds query parameters as the HTTP mirror names them; an absent value is left out, a list repeats its key. */
export type EventsQueryParams = Readonly<Record<string, string | number | boolean | readonly string[] | undefined>>;

/** Maps an `EventQuery` to its query parameters (`from`, `to`, `kind`, … `cursor`, `limit`). */
export function eventQueryParams(query: EventQuery = {}): EventsQueryParams {
  return {
    from: query.from,
    to: query.to,
    kind: query.kind,
    country: query.country,
    city: query.city,
    text: query.text,
    status: query.status,
    review: query.review,
    deadlineBefore: query.deadlineBefore,
    includeUndated: query.includeUndated,
    cursor: query.cursor,
    limit: query.limit,
  };
}

/** Maps a `DigestRunQuery` to its query parameters. */
export function digestRunQueryParams(query: DigestRunQuery = {}): EventsQueryParams {
  return { cursor: query.cursor, limit: query.limit };
}

/** Maps the filters of a `TrackingQuery`, which the list and its facets both take; lists repeat their key. */
function trackingFilterParams(query: TrackingQuery): EventsQueryParams {
  const list = (values: readonly string[] | undefined) => (values?.length ? values : undefined);
  return {
    trackedOnly: query.trackedOnly,
    stage: query.stage,
    stages: list(query.stages),
    excludeStages: list(query.excludeStages),
    priority: query.priority,
    from: query.from,
    to: query.to,
    kind: query.kind,
    kinds: list(query.kinds),
    city: query.city,
    cities: list(query.cities),
    status: query.status,
    sources: list(query.sources),
    publishedSince: query.publishedSince,
    country: query.country,
    countries: list(query.countries),
    excludeCountries: list(query.excludeCountries),
    includeUnknownCountry: query.includeUnknownCountry || undefined,
    text: query.text,
    deadlineBefore: query.deadlineBefore,
    includeUndated: query.includeUndated,
  };
}

/** Maps a `TrackingQuery` to its query parameters; the default sort (`starts`) is left out. */
export function trackingQueryParams(query: TrackingQuery = {}): EventsQueryParams {
  return {
    ...trackingFilterParams(query),
    sort: query.sort === 'starts' ? undefined : query.sort,
    cursor: query.cursor,
    limit: query.limit,
  };
}

/** Maps a `TrackingQuery` to the facets' query parameters: the list's filters, with no sort, cursor or limit. */
export function trackingFacetsQueryParams(query: TrackingQuery = {}): EventsQueryParams {
  return trackingFilterParams(query);
}

/** Maps a `TrackingSummaryQuery` to its query parameters (`today`, repeated `deadlineKinds`). */
export function trackingSummaryQueryParams(query: TrackingSummaryQuery): EventsQueryParams {
  return { today: query.today, deadlineKinds: query.deadlineKinds?.length ? query.deadlineKinds : undefined };
}

/** Maps an `ObservationQuery` to its query parameters. */
export function observationQueryParams(query: ObservationQuery = {}): EventsQueryParams {
  return { state: query.state, eventId: query.eventId, cursor: query.cursor, limit: query.limit };
}

/** Maps a `ReviewQuery` to its query parameters. */
export function reviewQueryParams(query: ReviewQuery = {}): EventsQueryParams {
  return { cursor: query.cursor, limit: query.limit };
}

/** Writes query parameters as a query string with its `?`, or an empty string when none is set. */
export function eventsQueryString(params: EventsQueryParams): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    for (const item of Array.isArray(value) ? (value as readonly string[]) : [value]) {
      if (item !== undefined && item !== '') search.append(key, String(item));
    }
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}
