import { eventsText } from './EventsMessages';
import type { EventsLocale } from './EventsLocale';

/** Holds the label props the fe sdk's `FacetBar` takes, written for the events feed (`searchLabel` … `matchLabel`). */
export interface EventsFacetBarLabels {
  readonly searchLabel: string;
  readonly searchPlaceholder: string;
  readonly filtersLabel: string;
  readonly clearAllLabel: string;
  readonly anyLabel: string;
  readonly selectedLabel: string;
  readonly removeLabel: string;
  readonly removeToggleLabel: string;
  readonly matchLabel: string;
}

/**
 * Writes the label props the feed gives its `FacetBar`, in `locale`, so a card that hosts the bar on a page reads the
 * same words: `v-bind="eventsFacetBarLabels(locale)"`. The `{count}`, `{facet}`, `{value}`, `{matched}` and `{total}`
 * placeholders stay for the bar to fill.
 */
export function eventsFacetBarLabels(locale: EventsLocale = 'en'): EventsFacetBarLabels {
  return {
    searchLabel: eventsText('searchEvents', locale),
    searchPlaceholder: eventsText('searchPlaceholder', locale),
    filtersLabel: eventsText('filters', locale),
    clearAllLabel: eventsText('clearAll', locale),
    anyLabel: eventsText('anyValue', locale),
    selectedLabel: eventsText('selectedCount', locale, { count: '{count}' }),
    removeLabel: eventsText('removeFilter', locale, { facet: '{facet}', value: '{value}' }),
    removeToggleLabel: eventsText('removeToggleFilter', locale, { facet: '{facet}' }),
    matchLabel: eventsText('matchOf', locale, { matched: '{matched}', total: '{total}' }),
  };
}
