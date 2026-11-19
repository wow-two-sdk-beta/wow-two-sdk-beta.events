import type { LocalizedText } from '../../contracts';

/** Lists the languages the kit's texts and dates are written in. */
export const EventsLocaleValues = ['en', 'ru', 'uz'] as const;
export type EventsLocale = (typeof EventsLocaleValues)[number];

/** Reads a locale tag (`ru-RU`, `uz`, …) as one of the kit's languages, English when it has none of them. */
export function toEventsLocale(tag: string | null | undefined): EventsLocale {
  const language = (tag ?? '').toLowerCase().split(/[-_]/)[0];
  return EventsLocaleValues.find((locale) => locale === language) ?? 'en';
}

/** Holds a localized text's picked value with the key it was written under (`ru-Cyrl`, `en`, `und`). */
export interface PickedText {
  readonly key: string;
  readonly value: string;
}

const languageOf = (key: string) => key.split('-')[0]?.toLowerCase() ?? '';

/**
 * Finds a text in one language by its prefix: the exact key, then the bare language, then its Latin script (`-Latn`),
 * then the first key of that language. Blank values never answer.
 */
function inLanguage(text: LocalizedText, language: string): PickedText | null {
  const root = languageOf(language);
  const keys = Object.keys(text).filter((key) => languageOf(key) === root && (text[key] ?? '').trim() !== '');
  const key =
    keys.find((item) => item.toLowerCase() === language.toLowerCase()) ??
    keys.find((item) => item.toLowerCase() === root) ??
    keys.find((item) => /-latn$/i.test(item)) ??
    keys[0];
  return key === undefined ? null : { key, value: (text[key] ?? '').trim() };
}

/**
 * Picks a localized text's value for `locale` with the key it came from, matching by language prefix (`ru` finds
 * `ru-Cyrl`, `uz` finds `uz-Latn`): that language, then the unknown-language value (`und`), then English, then the first
 * one written. Null when the text has none.
 */
export function pickTextEntry(text: LocalizedText | null | undefined, locale: string): PickedText | null {
  if (!text) return null;
  const first = Object.keys(text).find((key) => (text[key] ?? '').trim() !== '');
  return (
    inLanguage(text, locale) ??
    inLanguage(text, 'und') ??
    inLanguage(text, 'en') ??
    (first === undefined ? null : { key: first, value: (text[first] ?? '').trim() })
  );
}

/** Picks a localized text's value for `locale`, as {@link pickTextEntry} does; empty when the text has none. */
export function pickText(text: LocalizedText | null | undefined, locale: EventsLocale): string {
  return pickTextEntry(text, locale)?.value ?? '';
}

/**
 * Returns the tag (`RU`, `UZ`) shown beside a text written in another language than `locale`; null when it is in
 * `locale`'s language, in the unknown language (`und`), or empty.
 */
export function textLanguageTag(text: LocalizedText | null | undefined, locale: EventsLocale): string | null {
  const language = languageOf(pickTextEntry(text, locale)?.key ?? '');
  return !language || language === 'und' || language === locale ? null : language.toUpperCase();
}
