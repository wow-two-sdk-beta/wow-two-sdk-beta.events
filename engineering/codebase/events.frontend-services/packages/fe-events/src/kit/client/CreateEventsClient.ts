import { createApiClient, type ApiDecoder, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';
import {
  EventsRoutes,
  eventQueryParams,
  observationQueryParams,
  reviewQueryParams,
  trackingQueryParams,
  type CatalogModel,
  type CatalogSpec,
  type EventImportBundle,
  type EventImportReport,
  type EventModel,
  type EventPage,
  type EventQuery,
  type EventSpec,
  type EventsQueryParams,
  type IntakeStatusModel,
  type MergeDecision,
  type ObservationModel,
  type ObservationPage,
  type ObservationQuery,
  type ReviewDecision,
  type ReviewPage,
  type ReviewQuery,
  type TrackedEventModel,
  type TrackingHistoryEntryModel,
  type TrackingModel,
  type TrackingPage,
  type TrackingQuery,
  type TrackingSpec,
  type TrackingSummaryModel,
  type WorkflowModel,
  type WorkflowSpec,
} from '../../contracts';
import { EventsDecoders } from './EventsDecoders';
import { toEventsFailure, type EventsFailure } from './EventsFailure';

/** The header an unsafe request of a BFF session echoes its antiforgery token in. */
export const EventsCsrfHeader = 'X-Csrf-Token';

/** Configures where the Events HTTP mirror is reached. */
export interface EventsClientOptions {
  /**
   * The prefix the contract's routes are appended to: empty for the same origin's service root, the host app's proxy
   * prefix (`/api/desk/events`), or the service's URL. A trailing slash is ignored.
   */
  readonly baseUrl?: string;
  /** Replaces the global fetch, for tests or a custom transport. */
  readonly fetch?: typeof globalThis.fetch;
  /** Whether the browser sends cookies; the default sends them same-origin only. */
  readonly credentials?: RequestCredentials;
  /** Supplies the BFF session's antiforgery token, sent as `X-Csrf-Token` on every unsafe request. */
  readonly csrfToken?: () => string | null | undefined;
  /** Supplies a bearer token, for a host that calls the service with one. */
  readonly getAuthToken?: () => string | null | Promise<string | null>;
  /** Hears every 401, so a host can check its session and offer the sign-in again. */
  readonly onSignInRequired?: (failure: EventsFailure) => void;
}

/** Resolves an Events call: its answer's `data`, or the failure mapped from its status. */
export type EventsResult<T> = Promise<Result<T, EventsFailure>>;

/** Calls every route of the Events HTTP mirror; each method unwraps the `{ data }` envelope. */
export interface EventsClient {
  listCatalogs(signal?: AbortSignal): EventsResult<readonly CatalogModel[]>;
  getCatalog(catalog: string, signal?: AbortSignal): EventsResult<CatalogModel>;
  putCatalog(catalog: string, spec: CatalogSpec, signal?: AbortSignal): EventsResult<CatalogModel>;

  listEvents(catalog: string, query?: EventQuery, signal?: AbortSignal): EventsResult<EventPage>;
  getEvent(catalog: string, id: string, signal?: AbortSignal): EventsResult<EventModel>;
  /** Creates an event as a curator: it publishes. */
  createEvent(catalog: string, spec: EventSpec, signal?: AbortSignal): EventsResult<EventModel>;
  /** Proposes an event as a member: it lands as a candidate awaiting review. */
  proposeEvent(catalog: string, spec: EventSpec, signal?: AbortSignal): EventsResult<EventModel>;
  updateEvent(catalog: string, id: string, spec: EventSpec, signal?: AbortSignal): EventsResult<EventModel>;
  archiveEvent(catalog: string, id: string, expectedRevision: number, signal?: AbortSignal): EventsResult<EventModel>;
  /** Reads the iCalendar file of the published events the query selects (no envelope). */
  exportCalendar(catalog: string, query?: EventQuery, signal?: AbortSignal): EventsResult<string>;

  listReview(catalog: string, query?: ReviewQuery, signal?: AbortSignal): EventsResult<ReviewPage>;
  acceptEvent(catalog: string, id: string, decision: ReviewDecision, signal?: AbortSignal): EventsResult<EventModel>;
  rejectEvent(catalog: string, id: string, expectedRevision: number, signal?: AbortSignal): EventsResult<EventModel>;
  mergeEvent(catalog: string, id: string, decision: MergeDecision, signal?: AbortSignal): EventsResult<EventModel>;
  applyChange(
    catalog: string,
    observationId: string,
    decision: ReviewDecision,
    signal?: AbortSignal,
  ): EventsResult<EventModel>;
  dismissChange(catalog: string, observationId: string, signal?: AbortSignal): EventsResult<ObservationModel>;
  listObservations(catalog: string, query?: ObservationQuery, signal?: AbortSignal): EventsResult<ObservationPage>;
  getObservation(catalog: string, id: string, signal?: AbortSignal): EventsResult<ObservationModel>;

  listTracking(catalog: string, query?: TrackingQuery, signal?: AbortSignal): EventsResult<TrackingPage>;
  /** Reads the caller's organization's tracking summary; `today` is `YYYY-MM-DD`. */
  getTrackingSummary(catalog: string, today: string, signal?: AbortSignal): EventsResult<TrackingSummaryModel>;
  getTracking(catalog: string, eventId: string, signal?: AbortSignal): EventsResult<TrackedEventModel>;
  putTracking(catalog: string, eventId: string, spec: TrackingSpec, signal?: AbortSignal): EventsResult<TrackingModel>;
  deleteTracking(
    catalog: string,
    eventId: string,
    expectedRevision: number,
    signal?: AbortSignal,
  ): EventsResult<TrackingModel>;
  getTrackingHistory(
    catalog: string,
    eventId: string,
    signal?: AbortSignal,
  ): EventsResult<readonly TrackingHistoryEntryModel[]>;

  importEvents(
    catalog: string,
    bundle: EventImportBundle,
    dryRun: boolean,
    signal?: AbortSignal,
  ): EventsResult<EventImportReport>;

  getWorkflow(signal?: AbortSignal): EventsResult<WorkflowModel>;
  putWorkflow(spec: WorkflowSpec, signal?: AbortSignal): EventsResult<WorkflowModel>;

  getIntakeStatus(signal?: AbortSignal): EventsResult<IntakeStatusModel>;
  pollIntake(signal?: AbortSignal): EventsResult<IntakeStatusModel>;
}

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE';

interface Call<T> {
  readonly method: Method;
  readonly route: string;
  readonly decode: ApiDecoder<T>;
  readonly query?: EventsQueryParams;
  readonly body?: unknown;
  readonly signal?: AbortSignal | undefined;
}

/** Creates a client for the Events HTTP mirror under `baseUrl` (the same origin's service root by default). */
export function createEventsClient(options: EventsClientOptions = {}): EventsClient {
  const base = (options.baseUrl ?? '').replace(/\/+$/, '');
  const api = createApiClient({
    baseUrl: base,
    ...(options.credentials !== undefined ? { credentials: options.credentials } : {}),
    ...(options.fetch !== undefined ? { fetch: options.fetch } : {}),
    ...(options.getAuthToken !== undefined ? { getAuthToken: options.getAuthToken } : {}),
  });

  function headersFor(method: Method): Record<string, string> | undefined {
    if (method === 'GET') return undefined;
    const token = options.csrfToken?.();
    return token ? { [EventsCsrfHeader]: token } : undefined;
  }

  async function send<T>(call: Call<T>): EventsResult<T> {
    const headers = headersFor(call.method);
    const result: Result<T, ApiFailure> = await api.request<T>(`/${call.route}`, {
      method: call.method,
      decode: call.decode,
      ...(call.query !== undefined ? { query: call.query } : {}),
      ...(call.body !== undefined ? { body: call.body } : {}),
      ...(call.signal !== undefined ? { signal: call.signal } : {}),
      ...(headers !== undefined ? { headers } : {}),
    });
    return result.ok ? result : ResultExtensions.fail(failed(result.failure));
  }

  function failed(failure: ApiFailure): EventsFailure {
    const mapped = toEventsFailure(failure);
    if (mapped.reason === 'signInRequired') options.onSignInRequired?.(mapped);
    return mapped;
  }

  const get = <T>(route: string, decode: ApiDecoder<T>, signal?: AbortSignal, query?: EventsQueryParams) =>
    send({ method: 'GET', route, decode, signal, ...(query ? { query } : {}) });
  const post = <T>(
    route: string,
    decode: ApiDecoder<T>,
    body: unknown,
    signal?: AbortSignal,
    query?: EventsQueryParams,
  ) => send({ method: 'POST', route, decode, body, signal, ...(query ? { query } : {}) });
  const put = <T>(route: string, decode: ApiDecoder<T>, body: unknown, signal?: AbortSignal) =>
    send({ method: 'PUT', route, decode, body, signal });
  const remove = <T>(route: string, decode: ApiDecoder<T>, query: EventsQueryParams, signal?: AbortSignal) =>
    send({ method: 'DELETE', route, decode, query, signal });
  const revision = (expectedRevision: number) => ({ expectedRevision });

  return {
    listCatalogs: (signal) => get(EventsRoutes.catalogs(), EventsDecoders.catalogs, signal),
    getCatalog: (catalog, signal) => get(EventsRoutes.catalog(catalog), EventsDecoders.catalog, signal),
    putCatalog: (catalog, spec, signal) => put(EventsRoutes.catalog(catalog), EventsDecoders.catalog, spec, signal),

    listEvents: (catalog, query, signal) =>
      get(EventsRoutes.events(catalog), EventsDecoders.eventPage, signal, eventQueryParams(query)),
    getEvent: (catalog, id, signal) => get(EventsRoutes.event(catalog, id), EventsDecoders.event, signal),
    createEvent: (catalog, spec, signal) => post(EventsRoutes.events(catalog), EventsDecoders.event, spec, signal),
    proposeEvent: (catalog, spec, signal) => post(EventsRoutes.candidates(catalog), EventsDecoders.event, spec, signal),
    updateEvent: (catalog, id, spec, signal) =>
      put(EventsRoutes.event(catalog, id), EventsDecoders.event, spec, signal),
    archiveEvent: (catalog, id, expectedRevision, signal) =>
      remove(EventsRoutes.event(catalog, id), EventsDecoders.event, revision(expectedRevision), signal),
    async exportCalendar(catalog, query, signal) {
      const result = await api.get(`/${EventsRoutes.calendar(catalog)}`, {
        response: 'text',
        headers: { Accept: 'text/calendar' },
        query: eventQueryParams(query),
        ...(signal !== undefined ? { signal } : {}),
      });
      return result.ok ? result : ResultExtensions.fail(failed(result.failure));
    },

    listReview: (catalog, query, signal) =>
      get(EventsRoutes.review(catalog), EventsDecoders.reviewPage, signal, reviewQueryParams(query)),
    acceptEvent: (catalog, id, decision, signal) =>
      post(EventsRoutes.accept(catalog, id), EventsDecoders.event, decision, signal),
    rejectEvent: (catalog, id, expectedRevision, signal) =>
      post(EventsRoutes.reject(catalog, id), EventsDecoders.event, undefined, signal, revision(expectedRevision)),
    mergeEvent: (catalog, id, decision, signal) =>
      post(EventsRoutes.merge(catalog, id), EventsDecoders.event, decision, signal),
    applyChange: (catalog, observationId, decision, signal) =>
      post(EventsRoutes.applyChange(catalog, observationId), EventsDecoders.event, decision, signal),
    dismissChange: (catalog, observationId, signal) =>
      post(EventsRoutes.dismissChange(catalog, observationId), EventsDecoders.observation, undefined, signal),
    listObservations: (catalog, query, signal) =>
      get(EventsRoutes.observations(catalog), EventsDecoders.observationPage, signal, observationQueryParams(query)),
    getObservation: (catalog, id, signal) =>
      get(EventsRoutes.observation(catalog, id), EventsDecoders.observation, signal),

    listTracking: (catalog, query, signal) =>
      get(EventsRoutes.tracking(catalog), EventsDecoders.trackingPage, signal, trackingQueryParams(query)),
    getTrackingSummary: (catalog, today, signal) =>
      get(EventsRoutes.trackingSummary(catalog), EventsDecoders.trackingSummary, signal, { today }),
    getTracking: (catalog, eventId, signal) =>
      get(EventsRoutes.trackedEvent(catalog, eventId), EventsDecoders.trackedEvent, signal),
    putTracking: (catalog, eventId, spec, signal) =>
      put(EventsRoutes.trackedEvent(catalog, eventId), EventsDecoders.tracking, spec, signal),
    deleteTracking: (catalog, eventId, expectedRevision, signal) =>
      remove(EventsRoutes.trackedEvent(catalog, eventId), EventsDecoders.tracking, revision(expectedRevision), signal),
    getTrackingHistory: (catalog, eventId, signal) =>
      get(EventsRoutes.trackingHistory(catalog, eventId), EventsDecoders.history, signal),

    importEvents: (catalog, bundle, dryRun, signal) =>
      post(EventsRoutes.imports(catalog), EventsDecoders.importReport, bundle, signal, { dryRun }),

    getWorkflow: (signal) => get(EventsRoutes.workflow(), EventsDecoders.workflow, signal),
    putWorkflow: (spec, signal) => put(EventsRoutes.workflow(), EventsDecoders.workflow, spec, signal),

    getIntakeStatus: (signal) => get(EventsRoutes.intake(), EventsDecoders.intake, signal),
    pollIntake: (signal) => post(EventsRoutes.intakePoll(), EventsDecoders.intake, undefined, signal),
  };
}
