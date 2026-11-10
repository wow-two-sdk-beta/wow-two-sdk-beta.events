import type { LocalizedText } from '../../contracts';

/** Lists the languages the kit's texts and dates are written in. */
export const EventsLocaleValues = ['en', 'ru', 'uz'] as const;
export type EventsLocale = (typeof EventsLocaleValues)[number];

/** Reads a locale tag (`ru-RU`, `uz`, …) as one of the kit's languages, English when it has none of them. */
export function toEventsLocale(tag: string | null | undefined): EventsLocale {
  const language = (tag ?? '').toLowerCase().split(/[-_]/)[0];
  return EventsLocaleValues.find((locale) => locale === language) ?? 'en';
}

/**
 * Picks a localized text's value for `locale`: that locale, then the unknown-language value (`und`), then English, then
 * the first one written. Empty when the text has none.
 */
export function pickText(text: LocalizedText | null | undefined, locale: EventsLocale): string {
  if (!text) return '';
  const value = text[locale] ?? text['und'] ?? text['en'] ?? Object.values(text).find((item) => item.trim() !== '');
  return value?.trim() ?? '';
}
