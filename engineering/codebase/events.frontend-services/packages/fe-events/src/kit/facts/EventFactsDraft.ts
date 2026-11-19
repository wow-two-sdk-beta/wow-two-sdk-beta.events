import { EventsConstants, type EventFacts, type EventStatus, type EventWhen } from '../../contracts';
import { isEventWhenValue } from '../dates/EventDates';
import type { EventsLocale } from '../text/EventsLocale';
import type { EventsMessageKey } from '../text/EventsMessages';
import { safeHttpUrl } from './EventFactsText';

/** Holds the facts a form edits as plain strings; every other fact passes through from the base unchanged. */
export interface EventFactsDraft {
  title: string;
  /** The locale the title is written under: the one it was read from, `und` for a new event. */
  titleLocale: string;
  kind: string;
  status: EventStatus;
  starts: EventWhen;
  ends: EventWhen;
  venue: string;
  city: string;
  country: string;
  organizer: string;
  website: string;
  /** The application deadline's day, `YYYY-MM-DD`; empty for none. */
  applicationDeadline: string;
  /** Where the event was found: a link that stands in for the website when it has none. */
  foundAt: string;
}

const unknownWhen: EventWhen = { value: null, precision: 'unknown', certainty: 'announced' };

function titleLocaleOf(facts: EventFacts | undefined, locale: EventsLocale): string {
  const keys = Object.keys(facts?.title ?? {});
  return [locale, EventsConstants.unknownLocale, 'en'].find((key) => keys.includes(key)) ?? keys[0] ?? 'und';
}

/** Finds the first application deadline's index in `facts`. */
const applicationIndex = (facts: EventFacts | undefined) =>
  (facts?.deadlines ?? []).findIndex((deadline) => deadline.kind === 'application');

/** Starts a draft from `facts`, or an empty one with the title in the unknown language. */
export function eventFactsDraft(facts: EventFacts | undefined, locale: EventsLocale): EventFactsDraft {
  const titleLocale = titleLocaleOf(facts, locale);
  const application = facts?.deadlines?.[applicationIndex(facts)];
  return {
    title: facts?.title[titleLocale] ?? '',
    titleLocale,
    kind: facts?.kind ?? '',
    status: facts?.status ?? 'scheduled',
    starts: facts?.starts ?? unknownWhen,
    ends: facts?.ends ?? unknownWhen,
    venue: facts?.place?.venue ?? '',
    city: facts?.place?.city ?? '',
    country: facts?.place?.country ?? '',
    organizer: facts?.organizer?.name ?? '',
    website: facts?.website ?? '',
    applicationDeadline: application?.at.precision === 'day' ? (application.at.value ?? '') : '',
    foundAt: '',
  };
}

const text = (value: string) => value.trim() || null;
const when = (value: EventWhen): EventWhen =>
  value.precision === 'unknown'
    ? { value: null, precision: 'unknown', certainty: value.certainty }
    : { ...value, value: value.value?.trim() ?? null };

/** Checks a draft against the contract's rules; returns the facts to send over `base`, or the problems found. */
export function validateEventFactsDraft(
  draft: EventFactsDraft,
  base?: EventFacts,
): { readonly facts: EventFacts | null; readonly problems: readonly EventsMessageKey[] } {
  const problems = new Set<EventsMessageKey>();
  const title = draft.title.trim();
  const country = draft.country.trim().toUpperCase();
  const website = draft.website.trim();
  const foundAt = (draft.foundAt ?? '').trim();
  const deadlineDay = (draft.applicationDeadline ?? '').trim();
  if (!title || title.length > EventsConstants.maxTitleLength) problems.add('titleRequired');
  if (deadlineDay && !isEventWhenValue(deadlineDay, 'day')) problems.add('dateShape');
  if (foundAt && (!safeHttpUrl(foundAt) || foundAt.length > EventsConstants.maxUrlLength)) problems.add('urlShape');
  for (const value of [draft.starts, draft.ends]) {
    if (value.precision !== 'unknown' && !isEventWhenValue(value.value ?? '', value.precision))
      problems.add('dateShape');
  }
  if (country && !/^[A-Z]{2}$/.test(country)) problems.add('countryShape');
  if (website && (!safeHttpUrl(website) || website.length > EventsConstants.maxUrlLength)) problems.add('urlShape');
  if (problems.size) return { facts: null, problems: [...problems] };

  const place = { ...(base?.place ?? {}), venue: text(draft.venue), city: text(draft.city), country: country || null };
  const hasPlace = Object.values(place).some((value) => value !== null && value !== undefined && value !== '');
  const organizer = text(draft.organizer);
  // The application deadline replaces the first one at day precision, or is added; clearing it removes that one only.
  const deadlines = [...(base?.deadlines ?? [])];
  const index = applicationIndex(base);
  const current = index >= 0 ? deadlines[index] : undefined;
  if (deadlineDay) {
    const at: EventWhen = { value: deadlineDay, precision: 'day', certainty: 'announced' };
    if (current) deadlines[index] = { ...current, at };
    else deadlines.push({ kind: 'application', at });
  } else if (current?.at.precision === 'day') deadlines.splice(index, 1);
  return {
    problems: [],
    facts: {
      ...(base ?? {}),
      title: { ...(base?.title ?? {}), [draft.titleLocale || EventsConstants.unknownLocale]: title },
      kind: text(draft.kind) ?? base?.kind ?? 'other',
      status: draft.status,
      starts: when(draft.starts),
      ends: draft.ends.precision === 'unknown' ? null : when(draft.ends),
      place: hasPlace ? place : null,
      organizer: organizer ? { ...(base?.organizer ?? {}), name: organizer } : null,
      website: website || foundAt || null,
      ...(deadlines.length || base?.deadlines ? { deadlines } : {}),
    },
  };
}
