import type {
  CatalogKindModel,
  DateCertainty,
  DatePrecision,
  DeadlineKind,
  EventAttendance,
  EventAudience,
  EventReviewState,
  EventStatus,
  ObservationState,
  OfferKind,
  TrackingPriority,
  WorkflowStageModel,
} from '../../contracts';
import type { EventsFailure } from '../client/EventsFailure';
import { pickText, type EventsLocale } from './EventsLocale';
import { eventsText, type EventsMessageKey } from './EventsMessages';

const reviewKeys: Record<EventReviewState, EventsMessageKey> = {
  candidate: 'reviewCandidate',
  published: 'reviewPublished',
  rejected: 'reviewRejected',
  archived: 'reviewArchived',
};
const statusKeys: Record<EventStatus, EventsMessageKey> = {
  scheduled: 'statusScheduled',
  postponed: 'statusPostponed',
  rescheduled: 'statusRescheduled',
  cancelled: 'statusCancelled',
  movedOnline: 'statusMovedOnline',
};
const attendanceKeys: Record<EventAttendance, EventsMessageKey> = {
  offline: 'attendanceOffline',
  online: 'attendanceOnline',
  mixed: 'attendanceMixed',
};
const audienceKeys: Record<EventAudience, EventsMessageKey> = {
  public: 'audiencePublic',
  trade: 'audienceTrade',
  inviteOnly: 'audienceInviteOnly',
};
const deadlineKeys: Record<DeadlineKind, EventsMessageKey> = {
  application: 'deadlineApplication',
  registration: 'deadlineRegistration',
  earlyBird: 'deadlineEarlyBird',
  submission: 'deadlineSubmission',
  payment: 'deadlinePayment',
  other: 'deadlineOther',
};
const offerKeys: Record<OfferKind, EventsMessageKey> = {
  ticket: 'offerTicket',
  exhibitor: 'offerExhibitor',
  sponsorship: 'offerSponsorship',
  submission: 'offerSubmission',
  other: 'offerOther',
};
const priorityKeys: Record<TrackingPriority, EventsMessageKey> = {
  high: 'priorityHigh',
  normal: 'priorityNormal',
  low: 'priorityLow',
};
const precisionKeys: Record<DatePrecision, EventsMessageKey> = {
  unknown: 'precisionUnknown',
  year: 'precisionYear',
  month: 'precisionMonth',
  day: 'precisionDay',
  minute: 'precisionMinute',
};
const certaintyKeys: Record<DateCertainty, EventsMessageKey> = {
  announced: 'certaintyAnnounced',
  confirmed: 'certaintyConfirmed',
  estimated: 'certaintyEstimated',
};
const observationKeys: Record<ObservationState, EventsMessageKey> = {
  applied: 'observationApplied',
  pending: 'observationPending',
  superseded: 'observationSuperseded',
  quarantined: 'observationQuarantined',
  recorded: 'observationRecorded',
  accepted: 'observationAccepted',
  dismissed: 'observationDismissed',
  ignored: 'observationIgnored',
};

/** Labels a value from one of the contract's value sets, falling back to the raw value for one this kit does not know. */
function label<T extends string>(keys: Record<T, EventsMessageKey>, value: T, locale: EventsLocale): string {
  const key = keys[value] as EventsMessageKey | undefined;
  return key ? eventsText(key, locale) : value;
}

/** Labels the contract's value sets in the kit's languages. */
export const EventsLabels = {
  review: (value: EventReviewState, locale: EventsLocale) => label(reviewKeys, value, locale),
  status: (value: EventStatus, locale: EventsLocale) => label(statusKeys, value, locale),
  attendance: (value: EventAttendance, locale: EventsLocale) => label(attendanceKeys, value, locale),
  audience: (value: EventAudience, locale: EventsLocale) => label(audienceKeys, value, locale),
  deadline: (value: DeadlineKind, locale: EventsLocale) => label(deadlineKeys, value, locale),
  offer: (value: OfferKind, locale: EventsLocale) => label(offerKeys, value, locale),
  priority: (value: TrackingPriority, locale: EventsLocale) => label(priorityKeys, value, locale),
  precision: (value: DatePrecision, locale: EventsLocale) => label(precisionKeys, value, locale),
  certainty: (value: DateCertainty, locale: EventsLocale) => label(certaintyKeys, value, locale),
  observation: (value: ObservationState, locale: EventsLocale) => label(observationKeys, value, locale),
  /** Labels a catalog kind by the catalog's own label, or its key. */
  kind: (value: string | undefined, kinds: readonly CatalogKindModel[] | undefined, locale: EventsLocale) => {
    const kind = kinds?.find((item) => item.key === value);
    return pickText(kind?.label, locale) || value || '';
  },
  /** Labels a workflow stage by the organization's own label, or its key. */
  stage: (value: string, stages: readonly WorkflowStageModel[] | undefined, locale: EventsLocale) =>
    pickText(stages?.find((item) => item.key === value)?.label, locale) || value,
} as const;

/** Names what a screen needs when it is refused, so a 403 says which permission is missing. */
export type EventsFailureContext = 'read' | 'review' | 'admin';

/** Writes a failure for a person: the reason's own message, with the problem's detail on a validation failure. */
export function eventsFailureMessage(
  failure: EventsFailure,
  locale: EventsLocale,
  context: EventsFailureContext = 'read',
): string {
  switch (failure.reason) {
    case 'validation':
      return failure.detail
        ? `${eventsText('failureValidation', locale)} ${failure.detail}`
        : eventsText('failureValidation', locale);
    case 'signInRequired':
      return eventsText('failureSignIn', locale);
    case 'forbidden':
      if (context === 'review') return eventsText('failureReviewForbidden', locale);
      if (context === 'admin') return eventsText('failureAdminForbidden', locale);
      return eventsText('failureForbidden', locale);
    case 'notFound':
      return eventsText('failureNotFound', locale);
    case 'conflict':
      return eventsText('failureConflict', locale);
    case 'unavailable':
      return eventsText('failureUnavailable', locale);
    case 'protocol':
      return eventsText('failureProtocol', locale);
    case 'cancelled':
      return '';
    default:
      return eventsText('failureUnexpected', locale);
  }
}
