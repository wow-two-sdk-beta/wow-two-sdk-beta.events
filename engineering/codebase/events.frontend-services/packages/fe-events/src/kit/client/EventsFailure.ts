import type { ApiFailure } from '@wow-two-beta/fe/foundation/http';

/** Says why an Events call failed, read from the HTTP mirror's status table. */
export const EventsFailureReason = {
  /** 400: the request broke a contract rule; `fieldErrors` names the fields when the problem body does. */
  Validation: 'validation',
  /** 401: no session or token; the person signs in. */
  SignInRequired: 'signInRequired',
  /** 403: the scope or the person's permission is missing, or the caller does not own the catalog. */
  Forbidden: 'forbidden',
  /** 404: the catalog, event, observation or tracking does not exist for this caller. */
  NotFound: 'notFound',
  /** 409: the record changed since it was read, the key is taken, or the review state does not allow the action. */
  Conflict: 'conflict',
  /** The service did not answer: network, timeout, or 502/503/504. */
  Unavailable: 'unavailable',
  /** The caller aborted the request. */
  Cancelled: 'cancelled',
  /** The answer was not the contract's shape. */
  Protocol: 'protocol',
  /** Anything else. */
  Unexpected: 'unexpected',
} as const;
export type EventsFailureReason = (typeof EventsFailureReason)[keyof typeof EventsFailureReason];

/** Represents a failed Events call: the fe sdk's failure plus its contract reason and the problem body's detail. */
export interface EventsFailure extends ApiFailure {
  readonly reason: EventsFailureReason;
  /** The problem body's `detail` (or `title`); never rendered without the reason's own message beside it. */
  readonly detail: string | null;
  /** The problem body's `errors`: field → messages. */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
}

function reasonOf(failure: ApiFailure): EventsFailureReason {
  switch (failure.code) {
    case 'cancelled':
      return EventsFailureReason.Cancelled;
    case 'timeout':
    case 'transport':
      return EventsFailureReason.Unavailable;
    case 'protocol':
      return EventsFailureReason.Protocol;
    case 'validation':
      return EventsFailureReason.Validation;
    default:
      break;
  }
  switch (failure.status) {
    case 400:
    case 422:
      return EventsFailureReason.Validation;
    case 401:
      return EventsFailureReason.SignInRequired;
    case 403:
      return EventsFailureReason.Forbidden;
    case 404:
      return EventsFailureReason.NotFound;
    case 409:
      return EventsFailureReason.Conflict;
    case 429:
    case 502:
    case 503:
    case 504:
      return EventsFailureReason.Unavailable;
    default:
      return EventsFailureReason.Unexpected;
  }
}

function detailOf(problem: ApiFailure['problem']): string | null {
  const value = problem?.['detail'] ?? problem?.['title'];
  return typeof value === 'string' && value.trim() ? value : null;
}

function fieldErrorsOf(problem: ApiFailure['problem']): Record<string, readonly string[]> {
  const errors = problem?.['errors'];
  if (errors === null || typeof errors !== 'object' || Array.isArray(errors)) return {};
  const result: Record<string, readonly string[]> = {};
  for (const [field, messages] of Object.entries(errors)) {
    if (Array.isArray(messages)) result[field] = messages.filter((item): item is string => typeof item === 'string');
  }
  return result;
}

/** Maps an fe sdk failure to an Events failure, by its code first and its HTTP status second. */
export function toEventsFailure(failure: ApiFailure): EventsFailure {
  return {
    ...failure,
    reason: reasonOf(failure),
    detail: detailOf(failure.problem),
    fieldErrors: fieldErrorsOf(failure.problem),
  };
}
