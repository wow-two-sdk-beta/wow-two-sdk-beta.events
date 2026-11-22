import { eventsText } from './EventsMessages';
import type { EventsLocale } from './EventsLocale';

/**
 * Holds the strings a page's own filter bar needs, written for the events feed: the search box (`searchLabel`,
 * `searchPlaceholder`), the "Filters" button and "Clear all", the picker's "Any" and `{count}` selected, the removable
 * chips' close buttons and the match pill (`matchLabel`, "{matched} of {total}").
 */
export interface EventsFilterLabels {
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
 * Writes the filter bar's strings in `locale`, so a product's bar reads the words the events feed uses. The `{count}`,
 * `{facet}`, `{value}`, `{matched}` and `{total}` placeholders stay for the bar to fill.
 */
export function eventsFilterLabels(locale: EventsLocale = 'en'): EventsFilterLabels {
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
