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

/** Maps a `TrackingQuery` to its query parameters. */
export function trackingQueryParams(query: TrackingQuery = {}): EventsQueryParams {
  return {
    trackedOnly: query.trackedOnly,
    stage: query.stage,
    stages: query.stages?.length ? query.stages : undefined,
    excludeStages: query.excludeStages?.length ? query.excludeStages : undefined,
    priority: query.priority,
    from: query.from,
    to: query.to,
    kind: query.kind,
    country: query.country,
    countries: query.countries?.length ? query.countries : undefined,
    excludeCountries: query.excludeCountries?.length ? query.excludeCountries : undefined,
    includeUnknownCountry: query.includeUnknownCountry || undefined,
    text: query.text,
    deadlineBefore: query.deadlineBefore,
    includeUndated: query.includeUndated,
    cursor: query.cursor,
    limit: query.limit,
  };
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
