import { ApiFailureFactory, type ApiDecoder } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type {
  CatalogModel,
  EventImportReport,
  EventModel,
  EventPage,
  IntakeStatusModel,
  ObservationModel,
  ObservationPage,
  ReviewPage,
  TrackedEventModel,
  TrackingHistoryEntryModel,
  TrackingModel,
  TrackingPage,
  TrackingSummaryModel,
  WorkflowModel,
} from '../../contracts';

type Guard = (value: unknown) => boolean;

const isRecord = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value);
const isString = (value: unknown) => typeof value === 'string';
const isNumber = (value: unknown) => typeof value === 'number';
const isOptional = (guard: Guard) => (value: unknown) => value === undefined || value === null || guard(value);
const isArrayOf = (guard: Guard) => (value: unknown) => Array.isArray(value) && value.every(guard);
const has = (shape: Readonly<Record<string, Guard>>) => (value: unknown) =>
  isRecord(value) && Object.entries(shape).every(([key, guard]) => guard(value[key]));

/** Checks the members a screen relies on; the rest of a record passes through as the server wrote it. */
export const EventsShapes = {
  catalog: has({ slug: isString, name: isString, revision: isNumber }),
  event: has({
    id: isString,
    catalog: isString,
    facts: has({ title: isRecord }),
    review: isString,
    revision: isNumber,
  }),
  observation: has({ id: isString, catalog: isString, type: isString, state: isString, observedAtUtc: isString }),
  tracking: has({ id: isString, eventId: isString, stage: isString, priority: isString, revision: isNumber }),
  workflow: has({ stages: isArrayOf(has({ key: isString })), defaultStage: isString, revision: isNumber }),
  historyEntry: has({ toStage: isString, atUtc: isString }),
  intake: has({ sources: isArrayOf(has({ name: isString, catalog: isString })), candidates: isNumber }),
  importReport: has({ dryRun: (value: unknown) => typeof value === 'boolean', map: Array.isArray }),
} as const;

const page = (item: Guard) => has({ items: isArrayOf(item), nextCursor: isOptional(isString) });

/** Holds one decoder per answer shape. */
export const EventsDecoders = {
  catalog: decoder<CatalogModel>(EventsShapes.catalog),
  catalogs: decoder<readonly CatalogModel[]>(isArrayOf(EventsShapes.catalog)),
  event: decoder<EventModel>(EventsShapes.event),
  eventPage: decoder<EventPage>(page(EventsShapes.event)),
  observation: decoder<ObservationModel>(EventsShapes.observation),
  observationPage: decoder<ObservationPage>(page(EventsShapes.observation)),
  reviewPage: decoder<ReviewPage>(
    page(has({ event: EventsShapes.event, observations: isOptional(isArrayOf(EventsShapes.observation)) })),
  ),
  tracking: decoder<TrackingModel>(EventsShapes.tracking),
  trackedEvent: decoder<TrackedEventModel>(
    has({ event: EventsShapes.event, tracking: isOptional(EventsShapes.tracking) }),
  ),
  trackingPage: decoder<TrackingPage>(
    page(has({ event: EventsShapes.event, tracking: isOptional(EventsShapes.tracking) })),
  ),
  trackingSummary: decoder<TrackingSummaryModel>(has({ byStage: isRecord, upcomingDeadlines: Array.isArray })),
  history: decoder<readonly TrackingHistoryEntryModel[]>(isArrayOf(EventsShapes.historyEntry)),
  workflow: decoder<WorkflowModel>(EventsShapes.workflow),
  intake: decoder<IntakeStatusModel>(EventsShapes.intake),
  importReport: decoder<EventImportReport>(EventsShapes.importReport),
} as const;

/** Builds a decoder that accepts a value of the guarded shape and refuses anything else as a protocol failure. */
function decoder<T>(guard: Guard): ApiDecoder<T> {
  return (value) =>
    guard(value) ? ResultExtensions.ok(value as T) : ResultExtensions.fail(ApiFailureFactory.create('protocol'));
}
