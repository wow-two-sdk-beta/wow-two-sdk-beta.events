import type { EventQuery } from './EventModel';
import type { ObservationQuery, ReviewQuery } from './ReviewModels';
import type { TrackingQuery } from './TrackingModels';

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
  workflow: () => EventsRouteConstants.workflow,
  intake: () => EventsRouteConstants.intake,
  intakePoll: () => `${EventsRouteConstants.intake}/poll`,
} as const;

/** Holds query parameters as the HTTP mirror names them; an absent value is left out. */
export type EventsQueryParams = Readonly<Record<string, string | number | boolean | undefined>>;

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

/** Maps a `TrackingQuery` to its query parameters. */
export function trackingQueryParams(query: TrackingQuery = {}): EventsQueryParams {
  return {
    trackedOnly: query.trackedOnly,
    stage: query.stage,
    priority: query.priority,
    from: query.from,
    to: query.to,
    kind: query.kind,
    country: query.country,
    text: query.text,
    deadlineBefore: query.deadlineBefore,
    includeUndated: query.includeUndated,
    cursor: query.cursor,
    limit: query.limit,
  };
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
    if (value !== undefined && value !== '') search.append(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}
