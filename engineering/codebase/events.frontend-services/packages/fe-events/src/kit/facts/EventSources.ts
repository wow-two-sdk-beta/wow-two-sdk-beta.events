import type { EventModel, EventSourceModel } from '../../contracts';
import { safeHttpUrl } from './EventFactsText';

/** Says whether the event's listing left every source that reported one: each source `gone` or `unmatched`. */
export function listingRemoved(event: Pick<EventModel, 'sources'>): boolean {
  const sources = event.sources ?? [];
  return sources.length > 0 && sources.every((source) => source.state === 'gone' || source.state === 'unmatched');
}

/** Returns the source a card names ("via Afisha"): the first one with a name; null when none has one. */
export function attributedSource(event: Pick<EventModel, 'sources'>): EventSourceModel | null {
  return (event.sources ?? []).find((source) => source.name?.trim()) ?? null;
}

/** Names a source: its name, else its page's host; null when it has neither. */
export function sourceLabel(source: EventSourceModel): string | null {
  if (source.name?.trim()) return source.name.trim();
  const url = safeHttpUrl(source.url);
  return url ? new URL(url).hostname : null;
}
