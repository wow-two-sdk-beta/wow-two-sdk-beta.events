import type { EventFacts, EventWhen } from '../../contracts';
import { formatEventWhen } from '../../kit/dates/EventDates';
import { formatEventPlace } from '../../kit/facts/EventFactsText';
import { EventsLabels } from '../../kit/text/EventsLabels';
import { pickText, type EventsLocale } from '../../kit/text/EventsLocale';

/** Holds one fact a pending change proposes to change, written for a reviewer. */
export interface FactChange {
  readonly key: keyof EventFacts;
  readonly current: string;
  readonly proposed: string;
}

const empty = (value: unknown) =>
  value === undefined ||
  value === null ||
  value === '' ||
  (Array.isArray(value) && value.length === 0) ||
  (typeof value === 'object' && !Array.isArray(value) && Object.keys(value as object).length === 0);

/** Compares two values as the contract stores them, so key order and absent-versus-empty never count as a change. */
function same(left: unknown, right: unknown): boolean {
  if (empty(left) && empty(right)) return true;
  return JSON.stringify(sorted(left)) === JSON.stringify(sorted(right));
}
function sorted(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(sorted);
  if (value && typeof value === 'object')
    return Object.fromEntries(
      Object.entries(value)
        .filter(([, item]) => !empty(item))
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([key, item]) => [key, sorted(item)]),
    );
  return value;
}

function describe(key: keyof EventFacts, facts: EventFacts, locale: EventsLocale): string {
  const value = facts[key];
  if (empty(value)) return '—';
  switch (key) {
    case 'title':
    case 'summary':
      return pickText(value as EventFacts['title'], locale);
    case 'starts':
    case 'ends':
      return formatEventWhen(value as EventWhen, locale);
    case 'place':
      return formatEventPlace(facts, locale) || '—';
    case 'organizer':
      return facts.organizer?.name ?? '—';
    case 'status':
      return EventsLabels.status(facts.status ?? 'scheduled', locale);
    case 'images':
      // Sourced images stay links in text: evidence, never content.
      return (facts.images ?? []).map((image) => image.url).join('\n');
    default:
      return typeof value === 'string' ? value : JSON.stringify(value);
  }
}

/** Lists the facts `proposed` changes against `current`, in the contract's order. */
export function factChanges(current: EventFacts, proposed: EventFacts, locale: EventsLocale): FactChange[] {
  const keys = [...new Set([...Object.keys(current), ...Object.keys(proposed)])] as (keyof EventFacts)[];
  return keys
    .filter((key) => !same(current[key], proposed[key]))
    .map((key) => ({ key, current: describe(key, current, locale), proposed: describe(key, proposed, locale) }));
}
