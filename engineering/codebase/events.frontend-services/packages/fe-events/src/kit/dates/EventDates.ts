import type { DatePrecision, EventDeadline, EventModel, EventWhen } from '../../contracts';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';

/** Holds the parts of a date that are known; a part beyond `precision` is never filled in. */
export interface EventWhenParts {
  readonly precision: Exclude<DatePrecision, 'unknown'>;
  readonly year: number;
  /** 0-based month, from `month` precision on. */
  readonly month?: number;
  readonly day?: number;
  readonly hour?: number;
  readonly minute?: number;
}

/** Holds the calendar days a date covers: a month covers its whole month, a year its whole year. */
export interface EventWhenRange {
  /** `YYYY-MM-DD` of the first day covered. */
  readonly first: string;
  /** `YYYY-MM-DD` of the last day covered. */
  readonly last: string;
}

interface MonthNames {
  readonly short: readonly string[];
  readonly standalone: readonly string[];
  readonly long: readonly string[];
}

const english = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];
const englishShort = english.map((name) => name.slice(0, 3));
const uzbekShort = ['yan', 'fev', 'mar', 'apr', 'may', 'iyn', 'iyl', 'avg', 'sen', 'okt', 'noy', 'dek'];

/** Fixed tables keep dates identical in every browser, whatever its ICU data. */
const monthNames: Record<EventsLocale, MonthNames> = {
  en: { short: englishShort, standalone: englishShort, long: english },
  uz: {
    short: uzbekShort,
    standalone: uzbekShort,
    long: [
      'Yanvar',
      'Fevral',
      'Mart',
      'Aprel',
      'May',
      'Iyun',
      'Iyul',
      'Avgust',
      'Sentabr',
      'Oktabr',
      'Noyabr',
      'Dekabr',
    ],
  },
  ru: {
    short: ['янв.', 'февр.', 'мар.', 'апр.', 'мая', 'июн.', 'июл.', 'авг.', 'сент.', 'окт.', 'нояб.', 'дек.'],
    standalone: ['янв.', 'февр.', 'март', 'апр.', 'май', 'июнь', 'июль', 'авг.', 'сент.', 'окт.', 'нояб.', 'дек.'],
    long: [
      'Январь',
      'Февраль',
      'Март',
      'Апрель',
      'Май',
      'Июнь',
      'Июль',
      'Август',
      'Сентябрь',
      'Октябрь',
      'Ноябрь',
      'Декабрь',
    ],
  },
};

const rank: Record<DatePrecision, number> = { unknown: 0, year: 1, month: 2, day: 3, minute: 4 };
const byRank = ['unknown', 'year', 'month', 'day', 'minute'] as const;
const valuePattern = /^(\d{4})(?:-(\d{2})(?:-(\d{2})(?:T(\d{2}):(\d{2}))?)?)?$/;
const pad = (value: number) => String(value).padStart(2, '0');
const daysInMonth = (year: number, month: number) => new Date(Date.UTC(year, month + 1, 0)).getUTCDate();

/**
 * Reads what a date says, at the lower of its stated precision and its value's shape — a value never claims more than
 * its precision. Null when the date is unknown or unreadable.
 */
export function readEventWhen(when: EventWhen | null | undefined): EventWhenParts | null {
  if (!when || when.precision === 'unknown' || !when.value) return null;
  const match = valuePattern.exec(when.value.trim());
  if (!match) return null;
  const [, yearText, monthText, dayText, hourText, minuteText] = match;
  const shape = minuteText !== undefined ? 4 : dayText !== undefined ? 3 : monthText !== undefined ? 2 : 1;
  const known = Math.min(shape, rank[when.precision] ?? 0);
  if (known < 1) return null;

  const year = Number(yearText);
  const month = Number(monthText) - 1;
  const day = Number(dayText);
  const hour = Number(hourText);
  const minute = Number(minuteText);
  if (known >= 2 && !(month >= 0 && month <= 11)) return null;
  if (known >= 3 && !(day >= 1 && day <= daysInMonth(year, month))) return null;
  if (known >= 4 && !(hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)) return null;

  const precision = byRank[known] as EventWhenParts['precision'];
  return {
    precision,
    year,
    ...(known >= 2 ? { month } : {}),
    ...(known >= 3 ? { day } : {}),
    ...(known >= 4 ? { hour, minute } : {}),
  };
}

/** Says whether `value` has exactly the shape `precision` asks for: `2026`, `2026-03`, `2026-03-14`, `2026-03-14T19:00`. */
export function isEventWhenValue(value: string, precision: DatePrecision): boolean {
  if (precision === 'unknown') return value.trim() === '';
  const parts = readEventWhen({ value, precision, certainty: 'announced' });
  return parts !== null && parts.precision === precision && value.trim().length === shapeLength[precision];
}
const shapeLength: Record<Exclude<DatePrecision, 'unknown'>, number> = { year: 4, month: 7, day: 10, minute: 16 };

/** Returns the calendar days a date covers, or null when it is unknown. */
export function eventWhenRange(when: EventWhen | null | undefined): EventWhenRange | null {
  const parts = readEventWhen(when);
  if (!parts) return null;
  const { year } = parts;
  if (parts.month === undefined) return { first: `${year}-01-01`, last: `${year}-12-31` };
  const month = `${year}-${pad(parts.month + 1)}`;
  if (parts.day === undefined) return { first: `${month}-01`, last: `${month}-${pad(daysInMonth(year, parts.month))}` };
  const day = `${month}-${pad(parts.day)}`;
  return { first: day, last: day };
}

function formatParts(parts: EventWhenParts, locale: EventsLocale): string {
  const names = monthNames[locale];
  if (parts.month === undefined) return String(parts.year);
  if (parts.day === undefined) return `${names.standalone[parts.month]} ${parts.year}`;
  const day = `${parts.day} ${names.short[parts.month]} ${parts.year}`;
  return parts.hour === undefined ? day : `${day}, ${pad(parts.hour)}:${pad(parts.minute ?? 0)}`;
}

const expected = (text: string, locale: EventsLocale) => eventsText('expected', locale, { date: text });

/**
 * Writes one date exactly as precisely as it is known: `2026`, `Mar 2026`, `14 Mar 2026`, `14 Mar 2026, 19:00`; an
 * estimated date reads `≈ … (expected)`, an unknown one `Date not announced`.
 */
export function formatEventWhen(when: EventWhen | null | undefined, locale: EventsLocale): string {
  const parts = readEventWhen(when);
  if (!parts) return eventsText('dateNotAnnounced', locale);
  const text = formatParts(parts, locale);
  return when?.certainty === 'estimated' ? expected(text, locale) : text;
}

function formatSpanParts(start: EventWhenParts, end: EventWhenParts, locale: EventsLocale): string {
  const names = monthNames[locale];
  if (start.precision !== end.precision) return `${formatParts(start, locale)} – ${formatParts(end, locale)}`;
  const sameYear = start.year === end.year;
  const sameMonth = sameYear && start.month === end.month;
  switch (start.precision) {
    case 'year':
      return `${start.year}–${end.year}`;
    case 'month':
      return sameYear
        ? `${names.standalone[start.month ?? 0]} – ${names.standalone[end.month ?? 0]} ${start.year}`
        : `${formatParts(start, locale)} – ${formatParts(end, locale)}`;
    case 'day':
      if (sameMonth) return `${start.day}–${end.day} ${names.short[start.month ?? 0]} ${start.year}`;
      if (sameYear)
        return `${start.day} ${names.short[start.month ?? 0]} – ${end.day} ${names.short[end.month ?? 0]} ${start.year}`;
      return `${formatParts(start, locale)} – ${formatParts(end, locale)}`;
    default:
      if (sameMonth && start.day === end.day)
        return `${formatParts({ ...start, precision: 'day', hour: undefined }, locale)}, ${pad(start.hour ?? 0)}:${pad(start.minute ?? 0)}–${pad(end.hour ?? 0)}:${pad(end.minute ?? 0)}`;
      return `${formatParts(start, locale)} – ${formatParts(end, locale)}`;
  }
}

const whenKey = (parts: EventWhenParts) =>
  `${parts.year}-${pad((parts.month ?? -1) + 1)}-${pad(parts.day ?? 0)}T${pad(parts.hour ?? 0)}:${pad(parts.minute ?? 0)}`;

/**
 * Writes an event's span — `12–14 Oct 2026`, `28 Oct – 2 Nov 2026`, `Dec 2026 – Jan 2027` — each end as precisely as
 * it is known; `Dates not announced` when the start is unknown, `≈ … (expected)` when either end is estimated.
 */
export function formatEventSpan(
  starts: EventWhen | null | undefined,
  ends: EventWhen | null | undefined,
  locale: EventsLocale,
): string {
  const start = readEventWhen(starts);
  if (!start) return eventsText('datesNotAnnounced', locale);
  const end = readEventWhen(ends);
  const startRange = eventWhenRange(starts);
  const endRange = eventWhenRange(ends);
  const usable =
    end !== null &&
    startRange !== null &&
    endRange !== null &&
    endRange.last >= startRange.first &&
    !(end.precision === start.precision && whenKey(end) === whenKey(start));
  const text = usable && end ? formatSpanParts(start, end, locale) : formatParts(start, locale);
  const estimated = starts?.certainty === 'estimated' || (usable && ends?.certainty === 'estimated');
  return estimated ? expected(text, locale) : text;
}

/** Writes an event's dates from its facts. */
export function formatEventDates(event: Pick<EventModel, 'facts'>, locale: EventsLocale): string {
  return formatEventSpan(event.facts.starts, event.facts.ends, locale);
}

/** Orders two dates by the first day each covers, then the last; an unknown date sorts last. */
export function compareEventWhen(a: EventWhen | null | undefined, b: EventWhen | null | undefined): number {
  const left = eventWhenRange(a);
  const right = eventWhenRange(b);
  if (!left || !right) return left ? -1 : right ? 1 : 0;
  if (left.first !== right.first) return left.first < right.first ? -1 : 1;
  if (left.last !== right.last) return left.last < right.last ? -1 : 1;
  const leftTime = readEventWhen(a)?.hour ?? -1;
  const rightTime = readEventWhen(b)?.hour ?? -1;
  return leftTime - rightTime;
}

/** Sorts events by when they start; undated events close the list, in their original order. */
export function sortEventsByDate<T extends Pick<EventModel, 'facts'>>(events: readonly T[]): T[] {
  return events
    .map((event, index) => ({ event, index }))
    .sort((a, b) => compareEventWhen(a.event.facts.starts, b.event.facts.starts) || a.index - b.index)
    .map(({ event }) => event);
}

/** Holds the events that start in one month (`YYYY-MM`), one year with no month (`YYYY`), or with no date. */
export interface EventMonthGroup<T> {
  readonly key: string;
  readonly events: readonly T[];
}

/** Names the group of events with no date. */
export const undatedGroupKey = 'undated';

/**
 * Groups events by the month they start in, oldest first. An event known only to its year closes that year's months;
 * undated events close the list.
 */
export function groupEventsByMonth<T extends Pick<EventModel, 'facts'>>(events: readonly T[]): EventMonthGroup<T>[] {
  const groups = new Map<string, T[]>();
  for (const event of sortEventsByDate(events)) {
    const parts = readEventWhen(event.facts.starts);
    const key = !parts
      ? undatedGroupKey
      : parts.month === undefined
        ? String(parts.year)
        : `${parts.year}-${pad(parts.month + 1)}`;
    const group = groups.get(key);
    if (group) group.push(event);
    else groups.set(key, [event]);
  }
  const order = (key: string) => (key === undatedGroupKey ? '9999-99' : key.length === 4 ? `${key}-13` : key);
  return [...groups.entries()]
    .sort(([a], [b]) => order(a).localeCompare(order(b)))
    .map(([key, items]) => ({ key, events: items }));
}

/** Writes a group heading: `October 2026`, `2026, month not announced` or `Date not announced`. */
export function eventGroupHeading(key: string, locale: EventsLocale): string {
  if (key === undatedGroupKey) return eventsText('dateNotAnnounced', locale);
  if (key.length === 4) return eventsText('yearOnly', locale, { year: key });
  const month = Number(key.slice(5, 7)) - 1;
  return `${monthNames[locale].long[month] ?? key} ${key.slice(0, 4)}`;
}

/** Returns the browser's own calendar day as `YYYY-MM-DD`, so "today" matches the person's wall clock. */
export function localIsoDate(now: Date = new Date()): string {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

const utcDay = (date: string) =>
  Date.UTC(Number(date.slice(0, 4)), Number(date.slice(5, 7)) - 1, Number(date.slice(8, 10)));

/** Counts whole calendar days from `today` to `date` (both `YYYY-MM-DD`); negative once the date has passed. */
export function daysUntil(date: string, today: string): number {
  return Math.round((utcDay(date) - utcDay(today)) / 86_400_000);
}

/** Says how close a deadline is: within two weeks is urgent; passed ones stay on record. */
export type DeadlineTone = 'urgent' | 'upcoming' | 'passed';
export function deadlineTone(days: number): DeadlineTone {
  if (days < 0) return 'passed';
  return days <= 14 ? 'urgent' : 'upcoming';
}

/** Writes `Deadline today`, `1 day left`, `{count} days left` or `Deadline passed`. */
export function daysLeftLabel(days: number, locale: EventsLocale): string {
  if (days < 0) return eventsText('deadlinePassed', locale);
  if (days === 0) return eventsText('deadlineToday', locale);
  return days === 1 ? eventsText('oneDayLeft', locale) : eventsText('daysLeft', locale, { count: days });
}

/** Counts the days to a deadline known to its day; null for a deadline known only to its month or year. */
export function deadlineDaysLeft(deadline: Pick<EventDeadline, 'at'>, today: string): number | null {
  const parts = readEventWhen(deadline.at);
  const range = eventWhenRange(deadline.at);
  return parts && range && parts.day !== undefined ? daysUntil(range.first, today) : null;
}

/** Returns the nearest deadline whose days have not all passed, or null. */
export function nextDeadline<T extends Pick<EventDeadline, 'at'>>(
  deadlines: readonly T[] | undefined,
  today: string,
): T | null {
  const open = (deadlines ?? []).filter((deadline) => {
    const range = eventWhenRange(deadline.at);
    return range !== null && range.last >= today;
  });
  return open.sort((a, b) => compareEventWhen(a.at, b.at))[0] ?? null;
}

/** Says whether an event is over by `today`: its end (or its start) covers no day from `today` on. */
export function isEventPast(event: Pick<EventModel, 'facts'>, today: string): boolean {
  const range = eventWhenRange(event.facts.ends) ?? eventWhenRange(event.facts.starts);
  return range !== null && range.last < today;
}

/** Writes a UTC timestamp in the browser's time zone with the kit's month names: `12 Oct 2026, 14:05`. */
export function formatTimestamp(value: string | null | undefined, locale: EventsLocale): string {
  if (!value) return eventsText('never', locale);
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return `${date.getDate()} ${monthNames[locale].short[date.getMonth()]} ${date.getFullYear()}, ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
