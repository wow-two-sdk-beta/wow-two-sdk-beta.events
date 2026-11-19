import type { EventFacts, EventOffer } from '../../contracts';
import { pickText, textLanguageTag, type EventsLocale } from '../text/EventsLocale';
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

/** Returns an event's title in `locale`, matched by language prefix (`ru` finds `ru-Cyrl`). */
export function eventTitle(facts: Pick<EventFacts, 'title'>, locale: EventsLocale): string {
  return pickText(facts.title, locale);
}

/** Returns the tag (`RU`) shown beside an event's title written in another language than `locale`; null otherwise. */
export function eventTitleLanguage(facts: Pick<EventFacts, 'title'>, locale: EventsLocale): string | null {
  return textLanguageTag(facts.title, locale);
}

const groupDigits = (digits: string) => digits.replace(/\B(?=(\d{3})+(?!\d))/g, '\u00a0');

/** Writes an amount with its thousands grouped by a non-breaking space (`150 000`, `12.5`), the same in every browser. */
export function formatAmount(value: number): string {
  const [whole = '', fraction] = String(value).split('.');
  return fraction ? `${groupDigits(whole)}.${fraction}` : groupDigits(whole);
}

/**
 * Writes an offer's price: `Free`, `100 000 – 300 000 UZS`, `150 000 UZS`, `from 150 000 UZS`, `up to 300 000 UZS`, or
 * `Price not stated` — never a 0 for unknown. Amounts and their currency never split over two lines.
 */
export function formatOfferPrice(offer: EventOffer, locale: EventsLocale): string {
  if (offer.free) return eventsText('free', locale);
  const unit = offer.currency ? `\u00a0${offer.currency}` : '';
  const amount = (value: number) => `${formatAmount(value)}${unit}`;
  const min = offer.minPrice ?? null;
  const max = offer.maxPrice ?? null;
  if (min !== null && max !== null && min !== max)
    return `${formatAmount(min)}\u00a0–\u00a0${formatAmount(max)}${unit}`;
  if (min !== null && max !== null) return amount(min);
  if (min !== null) return eventsText('priceFrom', locale, { amount: amount(min) });
  if (max !== null) return eventsText('priceUpTo', locale, { amount: amount(max) });
  return eventsText('priceNotStated', locale);
}

/** Returns the ticket offer a card shows: the first ticket offer, else the first offer; null when the event has none. */
export function ticketOffer(facts: Pick<EventFacts, 'offers'>): EventOffer | null {
  const offers = facts.offers ?? [];
  return offers.find((offer) => offer.kind === 'ticket') ?? offers[0] ?? null;
}
