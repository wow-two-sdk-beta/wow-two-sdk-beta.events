import type { EventFacts, EventOffer } from '../../contracts';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';

/** Returns a normalized http(s) URL, or null for every other scheme (`javascript:`, `data:`, `file:` …). */
export function safeHttpUrl(value: string | null | undefined): string | null {
  const text = value?.trim();
  if (!text || !/^https?:\/\//i.test(text)) return null;
  try {
    const url = new URL(text);
    return (url.protocol === 'https:' || url.protocol === 'http:') && url.hostname ? url.href : null;
  } catch {
    return null;
  }
}

/** Names a country in `locale` by its ISO code, falling back to the code where the browser has no name. */
export function countryName(code: string | null | undefined, locale: EventsLocale): string | null {
  if (!code) return null;
  try {
    return new Intl.DisplayNames([locale], { type: 'region' }).of(code.toUpperCase()) ?? code;
  } catch {
    return code;
  }
}

/** Writes where an event happens: `Venue · Tashkent · Uzbekistan`, whichever parts are known. */
export function formatEventPlace(
  facts: Pick<EventFacts, 'place'>,
  locale: EventsLocale,
  options: { readonly venue?: boolean } = {},
): string {
  const place = facts.place;
  if (!place) return '';
  return [options.venue === false ? null : place.venue, place.city, countryName(place.country, locale)]
    .filter((part): part is string => Boolean(part && part.trim()))
    .join(' · ');
}

/** Returns an event's title in `locale`. */
export function eventTitle(facts: Pick<EventFacts, 'title'>, locale: EventsLocale): string {
  return pickText(facts.title, locale);
}

/** Writes an offer's price: `Free`, `100–250 UZS`, `from 100 UZS`, or `Price not stated` — never a 0 for unknown. */
export function formatOfferPrice(offer: EventOffer, locale: EventsLocale): string {
  if (offer.free) return eventsText('free', locale);
  const currency = offer.currency ? ` ${offer.currency}` : '';
  const format = (value: number) => new Intl.NumberFormat(locale).format(value);
  const min = offer.minPrice ?? null;
  const max = offer.maxPrice ?? null;
  if (min !== null && max !== null && min !== max) return `${format(min)}–${format(max)}${currency}`;
  if (min !== null) return `${format(min)}${currency}`;
  if (max !== null) return `≤ ${format(max)}${currency}`;
  return eventsText('priceNotStated', locale);
}
