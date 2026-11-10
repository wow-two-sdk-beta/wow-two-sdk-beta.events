import type { LocalizedText } from './Common';
import type { EventWhen } from './EventWhen';

/** Mirrors `EventAttendance`: where people attend. */
export const EventAttendanceValues = ['offline', 'online', 'mixed'] as const;
export type EventAttendance = (typeof EventAttendanceValues)[number];

/** Mirrors `EventAudience`: who may attend. */
export const EventAudienceValues = ['public', 'trade', 'inviteOnly'] as const;
export type EventAudience = (typeof EventAudienceValues)[number];

/** Mirrors `EventStatus`: whether the event still happens as announced. */
export const EventStatusValues = ['scheduled', 'postponed', 'rescheduled', 'cancelled', 'movedOnline'] as const;
export type EventStatus = (typeof EventStatusValues)[number];

/** Mirrors `DeadlineKind`. */
export const DeadlineKindValues = [
  'application',
  'registration',
  'earlyBird',
  'submission',
  'payment',
  'other',
] as const;
export type DeadlineKind = (typeof DeadlineKindValues)[number];

/** Mirrors `OfferKind`. */
export const OfferKindValues = ['ticket', 'exhibitor', 'sponsorship', 'submission', 'other'] as const;
export type OfferKind = (typeof OfferKindValues)[number];

/** Mirrors `EventOrganizer`. */
export interface EventOrganizer {
  readonly name: string;
  readonly website?: string | null;
}

/** Mirrors `EventPlace`; `country` is ISO 3166-1 alpha-2. */
export interface EventPlace {
  readonly venue?: string | null;
  readonly address?: string | null;
  readonly city?: string | null;
  readonly region?: string | null;
  readonly country?: string | null;
  readonly latitude?: number | null;
  readonly longitude?: number | null;
}

/** Mirrors `EventDeadline`. */
export interface EventDeadline {
  readonly kind: DeadlineKind;
  readonly at: EventWhen;
  readonly label?: string | null;
  readonly url?: string | null;
}

/** Mirrors `EventOffer`; a price is absent when unknown, never 0. */
export interface EventOffer {
  readonly kind: OfferKind;
  readonly name?: string | null;
  readonly minPrice?: number | null;
  readonly maxPrice?: number | null;
  readonly currency?: string | null;
  readonly free?: boolean;
  readonly terms?: string | null;
  readonly url?: string | null;
}

/** Mirrors `EventImage`: a curated image reference, never bytes. */
export interface EventImage {
  readonly url: string;
  readonly sourceUrl?: string | null;
  readonly captureId?: string | null;
  readonly attribution?: string | null;
}

/**
 * Mirrors `EventFacts`: what an event is. Every member but the title has a server default, so a spec may leave it out;
 * an answer carries it.
 */
export interface EventFacts {
  readonly key?: string | null;
  readonly title: LocalizedText;
  readonly summary?: LocalizedText;
  readonly kind?: string;
  readonly tags?: readonly string[];
  readonly organizer?: EventOrganizer | null;
  readonly place?: EventPlace | null;
  readonly attendance?: EventAttendance;
  readonly audience?: EventAudience;
  readonly status?: EventStatus;
  readonly starts?: EventWhen;
  readonly ends?: EventWhen | null;
  readonly timeZone?: string | null;
  readonly deadlines?: readonly EventDeadline[];
  readonly offers?: readonly EventOffer[];
  readonly website?: string | null;
  readonly ticketUrl?: string | null;
  readonly onlineUrl?: string | null;
  readonly images?: readonly EventImage[];
  readonly languages?: readonly string[];
}
