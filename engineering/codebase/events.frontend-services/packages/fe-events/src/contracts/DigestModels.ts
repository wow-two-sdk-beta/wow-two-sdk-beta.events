import type { EventDeadline, EventStatus } from './EventFacts';
import type { EventWhen } from './EventWhen';
import type { LocalizedText } from './Common';

/** Mirrors `DayOfWeek` on the wire, Monday first as a schedule reads. */
export const DigestDayValues = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'] as const;
export type DigestDay = (typeof DigestDayValues)[number];

/** Mirrors `DigestLanguageConstants`: the languages a digest is rendered in. */
export const DigestLanguageValues = ['uz-Latn', 'ru', 'en'] as const;
export type DigestLanguage = (typeof DigestLanguageValues)[number];

/** Mirrors `DigestChannelConstants`: the channels a host may register; a digest names one. */
export const DigestChannelConstants = { telegram: 'telegram', log: 'log' } as const;

/** Mirrors `DigestRunStatus`. */
export const DigestRunStatusValues = ['running', 'completed', 'partial', 'failed', 'empty', 'missed'] as const;
export type DigestRunStatus = (typeof DigestRunStatusValues)[number];

/** Mirrors `DigestRunTrigger`. */
export const DigestRunTriggerValues = ['schedule', 'manual'] as const;
export type DigestRunTrigger = (typeof DigestRunTriggerValues)[number];

/** Mirrors `DigestRecipientStatusConstants`: Identity's app notice words. */
export const DigestRecipientStatusValues = ['pending', 'sent', 'not_linked', 'muted', 'failed'] as const;
export type DigestRecipientStatus = (typeof DigestRecipientStatusValues)[number];

/** Mirrors `DigestSchedule`: days and local times (`HH:mm`) in an IANA zone. */
export interface DigestSchedule {
  readonly days: readonly DigestDay[];
  readonly times: readonly string[];
  readonly timeZone: string;
}

/** Mirrors `DigestSelection`: rules (any) and filters (all) over the catalog's published events. */
export interface DigestSelection {
  readonly newSinceLastRun?: boolean;
  readonly startingWithinDays?: number | null;
  readonly deadlineWithinDays?: number | null;
  readonly statuses?: readonly EventStatus[];
  /** The catalog's kinds (formats). */
  readonly kinds?: readonly string[];
  readonly cities?: readonly string[];
  readonly trackedOnly?: boolean;
}

/** Mirrors `DigestRecipient`: a person by Identity `sub`, or an organization role; an optional language. */
export interface DigestRecipient {
  readonly subject?: string | null;
  readonly role?: string | null;
  readonly language?: DigestLanguage | null;
}

/** Mirrors `DigestSpec`. */
export interface DigestSpec {
  readonly name: string;
  readonly enabled?: boolean;
  readonly channel?: string;
  readonly schedule: DigestSchedule;
  readonly selection?: DigestSelection;
  readonly recipients: readonly DigestRecipient[];
  readonly language?: DigestLanguage;
  readonly itemCap?: number;
  /** An absolute URL with `{id}`, `{key}` or `{catalog}`; none takes the host's template for the catalog. */
  readonly linkTemplate?: string | null;
  readonly expectedRevision?: number | null;
}

/** Mirrors `DigestModel`. */
export interface DigestModel {
  readonly id: string;
  readonly catalog: string;
  readonly name: string;
  readonly enabled: boolean;
  readonly channel: string;
  readonly schedule: DigestSchedule;
  readonly selection: DigestSelection;
  readonly recipients: readonly DigestRecipient[];
  readonly language: DigestLanguage;
  readonly itemCap: number;
  readonly linkTemplate?: string | null;
  readonly nextRunAtUtc?: string | null;
  readonly lastRunAtUtc?: string | null;
  readonly revision: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

/** Mirrors `DigestItemModel`: one event a digest lists, in no language. */
export interface DigestItemModel {
  readonly eventId: string;
  readonly key?: string | null;
  readonly title: LocalizedText;
  readonly starts: EventWhen;
  readonly ends?: EventWhen | null;
  readonly city?: string | null;
  readonly status?: EventStatus;
  readonly nextDeadline?: EventDeadline | null;
  readonly url?: string | null;
  readonly isNew?: boolean;
}

/** Mirrors `DigestTextModel`: the message in one language, Telegram's HTML subset (`b`, `i`, `a href`). */
export interface DigestTextModel {
  readonly language: DigestLanguage;
  readonly text: string;
  readonly recipients: number;
}

/** Mirrors `DigestRunRecipientModel`. */
export interface DigestRunRecipientModel {
  readonly subject: string;
  readonly language: DigestLanguage;
  readonly status: DigestRecipientStatus;
  readonly error?: string | null;
  readonly attempts: number;
  readonly updatedAtUtc: string;
}

/** Mirrors `DigestRunModel`: one occurrence (or send now) and each recipient's result. */
export interface DigestRunModel {
  readonly id: string;
  readonly digestId: string;
  readonly occurrenceAtUtc: string;
  readonly trigger: DigestRunTrigger;
  readonly status: DigestRunStatus;
  readonly matched: number;
  readonly selected: number;
  readonly eventIds?: readonly string[];
  readonly texts?: readonly DigestTextModel[];
  readonly recipients?: readonly DigestRunRecipientModel[];
  readonly notes?: readonly string[];
  readonly startedAtUtc: string;
  readonly finishedAtUtc?: string | null;
}

/** Mirrors `DigestRunPage`, newest first. */
export interface DigestRunPage {
  readonly items: readonly DigestRunModel[];
  readonly nextCursor?: string | null;
}

/** Mirrors `DigestRunQuery`. */
export interface DigestRunQuery {
  readonly cursor?: string;
  readonly limit?: number;
}

/** Mirrors `DigestPreviewModel`: what a send now at this instant sends, without sending. */
export interface DigestPreviewModel {
  readonly digestId: string;
  readonly asOfUtc: string;
  readonly matched: number;
  readonly items: readonly DigestItemModel[];
  readonly texts: readonly DigestTextModel[];
  readonly recipients: readonly DigestRunRecipientModel[];
  readonly notes?: readonly string[];
}
