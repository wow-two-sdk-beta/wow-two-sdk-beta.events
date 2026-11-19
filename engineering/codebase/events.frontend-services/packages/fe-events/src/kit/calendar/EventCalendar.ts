import { downloadText } from '@wow-two-beta/fe/foundation/files';
import type { EventModel, EventWhen } from '../../contracts';
import {
  eventWhenRange,
  readEventWhen,
  zonedInstant,
  zoneOffsetMinutes,
  type EventWhenParts,
} from '../dates/EventDates';
import { countryName, eventTitle, safeHttpUrl } from '../facts/EventFactsText';
import { EventsLabels } from '../text/EventsLabels';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';

/** Holds one event's local calendar file: its entries for the occurrence and the application deadline, when exact. */
export interface EventCalendarFile {
  readonly fileName: string;
  readonly content: string;
  readonly hasOccurrence: boolean;
  readonly hasDeadline: boolean;
}

/** Shapes one event's calendar file. */
export interface EventCalendarOptions {
  /** The file's stamp time; now by default. */
  readonly now?: Date;
  /** The file name; `event-{id}.ics` by default. */
  readonly fileName?: string;
  /** The calendar's `PRODID`. */
  readonly productId?: string;
  /** The suffix after `@` in each entry's `UID`, so a re-download replaces the entry instead of adding one. */
  readonly uidDomain?: string;
}

const text = (value: string) =>
  value
    .replace(/\\/g, '\\\\')
    .replace(/\r\n|\r|\n/g, '\\n')
    .replace(/[,;]/g, '\\$&');

/** Folds a content line at 75 UTF-8 octets, each continuation starting with a space. */
function fold(line: string): string {
  const encoder = new TextEncoder();
  let current = '';
  let size = 0;
  let folded = '';
  for (const character of line) {
    const bytes = encoder.encode(character).length;
    if (size + bytes > 75) {
      folded += `${current}\r\n`;
      current = ' ';
      size = 1;
    }
    current += character;
    size += bytes;
  }
  return folded + current;
}

const pad = (value: number) => String(value).padStart(2, '0');
const day = (iso: string) => iso.replace(/-/g, '');
const stamp = (date: Date) =>
  date
    .toISOString()
    .replace(/[-:]/g, '')
    .replace(/\.\d{3}Z$/, 'Z');
const offset = (minutes: number) =>
  `${minutes < 0 ? '-' : '+'}${pad(Math.floor(Math.abs(minutes) / 60))}${pad(Math.abs(minutes) % 60)}`;

function exclusiveEnd(iso: string): string {
  const date = new Date(`${iso}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return day(date.toISOString().slice(0, 10));
}

/** Reads a date known to its day (or minute) and not estimated; null otherwise. */
function exact(when: EventWhen | null | undefined): { parts: EventWhenParts; iso: string } | null {
  const parts = readEventWhen(when);
  const range = eventWhenRange(when);
  return parts && range && parts.day !== undefined ? { parts, iso: range.first } : null;
}

/**
 * Builds one event's calendar file (PG17): the occurrence when its dates are exact — all-day, or at its start time in its
 * zone (`DTSTART;TZID=…` with that zone's offset as its `VTIMEZONE`), floating local time without a zone — and a separate
 * all-day entry for its application deadline when exact. Null when neither is exact; estimated dates never enter it.
 */
export function createEventCalendar(
  event: EventModel,
  locale: EventsLocale,
  options: EventCalendarOptions = {},
): EventCalendarFile | null {
  const facts = event.facts;
  const start = exact(facts.starts);
  const end = facts.ends ? exact(facts.ends) : null;
  const hasOccurrence =
    start !== null && (!facts.ends || facts.ends.precision === 'unknown' || (end !== null && end.iso >= start.iso));
  const deadline = (facts.deadlines ?? [])
    .filter((item) => item.kind === 'application')
    .map((item) => exact(item.at))
    .find((item) => item !== null);
  if (!hasOccurrence && !deadline) return null;

  const domain = options.uidDomain ?? 'wow-two-events';
  const title = eventTitle(facts, locale);
  const website = safeHttpUrl(facts.website);
  const place = facts.place;
  const location = [place?.venue, place?.address, place?.city, countryName(place?.country, locale)]
    .filter((part): part is string => Boolean(part?.trim()))
    .join(', ');
  const description = [
    facts.organizer?.name ? `${eventsText('calendarOrganizer', locale)}: ${facts.organizer.name}` : null,
    website ? `${eventsText('website', locale)}: ${website}` : null,
  ]
    .filter(Boolean)
    .join('\n\n');
  const updated = new Date(event.updatedAtUtc);
  const metadata = [
    `DTSTAMP:${stamp(options.now ?? new Date())}`,
    `SEQUENCE:${Math.max(0, Math.trunc(event.revision))}`,
    ...(Number.isNaN(updated.getTime()) ? [] : [`LAST-MODIFIED:${stamp(updated)}`]),
    'TRANSP:TRANSPARENT',
  ];
  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    `PRODID:${options.productId ?? '-//WoW2//Events kit//EN'}`,
    'CALSCALE:GREGORIAN',
  ];

  if (hasOccurrence && start) {
    const endIso = end?.iso ?? start.iso;
    const times: string[] = [];
    const zoneLines: string[] = [];
    if (start.parts.precision === 'minute') {
      const zone = facts.timeZone?.trim() || null;
      const instant = zone ? zonedInstant(start.parts, zone) : null;
      const zoneOffset = zone && instant !== null ? zoneOffsetMinutes(zone, instant) : null;
      const property = (name: string) => (zone && zoneOffset !== null ? `${name};TZID=${zone}` : name);
      times.push(
        `${property('DTSTART')}:${day(start.iso)}T${pad(start.parts.hour ?? 0)}${pad(start.parts.minute ?? 0)}00`,
      );
      if (endIso > start.iso) times.push(`${property('DTEND')}:${exclusiveEnd(endIso)}T000000`);
      if (zone && zoneOffset !== null)
        zoneLines.push(
          'BEGIN:VTIMEZONE',
          `TZID:${zone}`,
          'BEGIN:STANDARD',
          'DTSTART:19700101T000000',
          `TZOFFSETFROM:${offset(zoneOffset)}`,
          `TZOFFSETTO:${offset(zoneOffset)}`,
          'END:STANDARD',
          'END:VTIMEZONE',
        );
    } else {
      times.push(`DTSTART;VALUE=DATE:${day(start.iso)}`, `DTEND;VALUE=DATE:${exclusiveEnd(endIso)}`);
    }
    lines.push(
      ...zoneLines,
      'BEGIN:VEVENT',
      `UID:${text(`event-${event.id}-occurrence@${domain}`)}`,
      ...metadata,
      ...times,
      `SUMMARY:${text(title)}`,
      ...(location ? [`LOCATION:${text(location)}`] : []),
      ...(description ? [`DESCRIPTION:${text(description)}`] : []),
      ...(website ? [`URL:${website}`] : []),
      'END:VEVENT',
    );
  }
  if (deadline) {
    lines.push(
      'BEGIN:VEVENT',
      `UID:${text(`event-${event.id}-application@${domain}`)}`,
      ...metadata,
      `DTSTART;VALUE=DATE:${day(deadline.iso)}`,
      `DTEND;VALUE=DATE:${exclusiveEnd(deadline.iso)}`,
      `SUMMARY:${text(`${EventsLabels.deadline('application', locale)}: ${title}`)}`,
      `DESCRIPTION:${text([eventsText('calendarDeadlineNote', locale), description].filter(Boolean).join('\n\n'))}`,
      ...(website ? [`URL:${website}`] : []),
      'END:VEVENT',
    );
  }
  lines.push('END:VCALENDAR');
  return {
    fileName: options.fileName ?? `event-${event.id}.ics`,
    content: `${lines.map(fold).join('\r\n')}\r\n`,
    hasOccurrence,
    hasDeadline: Boolean(deadline),
  };
}

/** Downloads one event's calendar file locally; it never connects to or changes a calendar account. */
export function downloadEventCalendar(file: EventCalendarFile): boolean {
  return downloadText(file.content, file.fileName, 'text/calendar;charset=utf-8');
}
