import { describe, expect, it } from 'vitest';
import {
  eventsQueryString,
  trackingFacetsQueryParams,
  trackingQueryParams,
  trackingSummaryQueryParams,
} from '../src/contracts';
import {
  addEventDays,
  createEventCalendar,
  eventDateRange,
  eventTitleLanguage,
  formatEventDates,
  formatEventWhen,
  formatOfferPrice,
  groupEventsByMonth,
  pickText,
  pickTextEntry,
  regionOfCountry,
  regionQuery,
  ticketOffer,
  type EventRegion,
} from '../src/kit';
import { eventModel } from './Fixtures';

const regions: EventRegion[] = [
  { key: 'local', label: { en: 'Local' }, countries: ['UZ'] },
  { key: 'regional', label: { en: 'Regional' }, countries: ['KZ', 'KG', 'TJ', 'TM', 'AF', 'AZ', 'RU'] },
  { key: 'international', label: { en: 'International' }, otherCountries: true },
];

describe('titles by language prefix (PG07)', () => {
  it('prefers the exact key, then the bare language, then Latin, then the first key', () => {
    const sourced = { 'ru-Cyrl': 'Выставка текстиля', en: 'Textile Exhibition' };

    expect(pickTextEntry(sourced, 'ru')).toEqual({ key: 'ru-Cyrl', value: 'Выставка текстиля' });
    expect(pickText(sourced, 'en')).toBe('Textile Exhibition');
    expect(pickText(sourced, 'uz')).toBe('Textile Exhibition');
    expect(pickText({ 'uz-Cyrl': 'Мода', 'uz-Latn': 'Moda' }, 'uz')).toBe('Moda');
    expect(pickText({ 'uz-Latn': 'Moda', uz: 'Moda (uz)' }, 'uz')).toBe('Moda (uz)');
    expect(pickText({ und: 'Show', ru: 'Шоу' }, 'en')).toBe('Show');
    expect(pickText({ fr: 'Défilé' }, 'en')).toBe('Défilé');
    expect(pickText({ en: '  ', ru: 'Шоу' }, 'en')).toBe('Шоу');
  });

  it('tags a title written in another language, never an undetermined one', () => {
    expect(eventTitleLanguage({ title: { 'ru-Cyrl': 'Выставка' } }, 'en')).toBe('RU');
    expect(eventTitleLanguage({ title: { 'ru-Cyrl': 'Выставка', en: 'Exhibition' } }, 'en')).toBeNull();
    expect(eventTitleLanguage({ title: { 'ru-Cyrl': 'Выставка' } }, 'ru')).toBeNull();
    expect(eventTitleLanguage({ title: { und: 'Show' } }, 'uz')).toBeNull();
  });
});

describe('prices (PG10)', () => {
  it('writes from, up to and ranges with grouped thousands that never split', () => {
    expect(formatOfferPrice({ kind: 'ticket', minPrice: 150000, currency: 'UZS' }, 'en')).toBe('from 150 000 UZS');
    expect(formatOfferPrice({ kind: 'ticket', maxPrice: 300000, currency: 'UZS' }, 'ru')).toBe('до 300 000 UZS');
    expect(formatOfferPrice({ kind: 'ticket', minPrice: 100000, maxPrice: 300000, currency: 'UZS' }, 'en')).toBe(
      '100 000 – 300 000 UZS',
    );
    expect(formatOfferPrice({ kind: 'ticket', minPrice: 150000, maxPrice: 150000, currency: 'UZS' }, 'uz')).toBe(
      '150 000 UZS',
    );
    expect(formatOfferPrice({ kind: 'ticket', free: true }, 'en')).toBe('Free');
  });

  it('shows the first ticket offer, else the first offer', () => {
    expect(
      ticketOffer({
        offers: [
          { kind: 'exhibitor', minPrice: 1 },
          { kind: 'ticket', minPrice: 2 },
        ],
      })?.minPrice,
    ).toBe(2);
    expect(ticketOffer({ offers: [{ kind: 'exhibitor', minPrice: 1 }] })?.minPrice).toBe(1);
    expect(ticketOffer({ offers: [] })).toBeNull();
  });
});

describe('dates (PG12, PG13)', () => {
  it('reads an estimated day or minute as its month, never a day', () => {
    const estimated = { value: '2027-04-12T19:00', precision: 'minute', certainty: 'estimated' } as const;

    expect(formatEventWhen(estimated, 'en')).toBe('≈ Apr 2027 (expected)');
    expect(groupEventsByMonth([eventModel('a', { starts: estimated })]).map((group) => group.key)).toEqual(['2027-04']);
    expect(formatEventWhen({ value: '2027-04-12', precision: 'day', certainty: 'announced' }, 'en')).toBe(
      '12 Apr 2027',
    );
  });

  it("names the start time's zone only when it differs from the viewer's", () => {
    const show = eventModel('z', {
      starts: { value: '2026-12-05T19:00', precision: 'minute', certainty: 'announced' },
      timeZone: 'Asia/Tashkent',
    });

    expect(formatEventDates(show, 'en', { viewerZone: 'Asia/Tashkent' })).toBe('5 Dec 2026, 19:00');
    expect(formatEventDates(show, 'en', { viewerZone: 'Asia/Karachi' })).toBe('5 Dec 2026, 19:00');
    expect(formatEventDates(show, 'en', { viewerZone: 'Europe/Berlin' })).toBe('5 Dec 2026, 19:00 GMT+5');
    expect(formatEventDates(eventModel('n', { starts: show.facts.starts }), 'en', { viewerZone: 'UTC' })).toBe(
      '5 Dec 2026, 19:00',
    );
  });
});

describe('regions (PG03, PG04)', () => {
  it('derives a region from a country, none without one', () => {
    expect(regionOfCountry('uz', regions)?.key).toBe('local');
    expect(regionOfCountry('KZ', regions)?.key).toBe('regional');
    expect(regionOfCountry('FR', regions)?.key).toBe('international');
    expect(regionOfCountry(null, regions)).toBeNull();
    expect(regionOfCountry('UZB', regions)).toBeNull();
  });

  it('selects a region by its countries, the rest by leaving every listed country out', () => {
    expect(regionQuery(regions[0], regions)).toEqual({ countries: ['UZ'] });
    expect(regionQuery(regions[2], regions)).toEqual({
      excludeCountries: ['UZ', 'KZ', 'KG', 'TJ', 'TM', 'AF', 'AZ', 'RU'],
      includeUnknownCountry: false,
    });
    expect(regionQuery(null, regions)).toEqual({});
  });

  it('writes country and stage lists as repeated keys (E21, PG02, PG14)', () => {
    expect(
      eventsQueryString(
        trackingQueryParams({
          countries: ['UZ', 'KZ'],
          excludeStages: ['skipped'],
          includeUnknownCountry: false,
          stages: [],
        }),
      ),
    ).toBe('?excludeStages=skipped&countries=UZ&countries=KZ');
    expect(eventsQueryString(trackingSummaryQueryParams({ today: '2026-11-10', deadlineKinds: ['application'] }))).toBe(
      '?today=2026-11-10&deadlineKinds=application',
    );
  });
});

describe('tracking query parameters (UI04)', () => {
  it('writes the city, kind and source lists as repeated keys, the default sort never, and a blank list nowhere', () => {
    expect(
      eventsQueryString(
        trackingQueryParams({
          kinds: ['fair', 'exhibition'],
          cities: ['tashkent'],
          sources: [],
          status: 'cancelled',
          publishedSince: '2026-11-10T09:00:00Z',
          sort: 'starts',
        }),
      ),
    ).toBe('?kinds=fair&kinds=exhibition&cities=tashkent&status=cancelled&publishedSince=2026-11-10T09%3A00%3A00Z');
    expect(eventsQueryString(trackingQueryParams({ sort: 'added', limit: 20 }))).toBe('?sort=added&limit=20');
  });

  it("writes the facets the list's filters only", () => {
    const query = {
      sources: ['curated'],
      deadlineBefore: '2026-12-31',
      sort: 'deadline',
      cursor: 'c',
      limit: 9,
    } as const;

    expect(eventsQueryString(trackingFacetsQueryParams(query))).toBe('?sources=curated&deadlineBefore=2026-12-31');
    expect(eventsQueryString(trackingFacetsQueryParams())).toBe('');
  });
});

describe("one event's calendar (PG17)", () => {
  const now = new Date('2026-11-19T10:00:00Z');

  it('writes a timed start in its zone and the application deadline as its own all-day entry', () => {
    const show = eventModel('e1', {
      title: { en: 'Designers, Show; 2026' },
      starts: { value: '2026-12-05T19:00', precision: 'minute', certainty: 'announced' },
      timeZone: 'Asia/Tashkent',
      place: { venue: 'Palace', city: 'Tashkent', country: 'UZ' },
      deadlines: [{ kind: 'application', at: { value: '2026-11-25', precision: 'day', certainty: 'announced' } }],
    });

    const file = createEventCalendar(show, 'en', { now })!;

    expect(file.fileName).toBe('event-e1.ics');
    expect(file.hasOccurrence && file.hasDeadline).toBe(true);
    expect(file.content).toContain('BEGIN:VTIMEZONE\r\nTZID:Asia/Tashkent\r\n');
    expect(file.content).toContain('TZOFFSETTO:+0500');
    expect(file.content).toContain('DTSTART;TZID=Asia/Tashkent:20261205T190000');
    expect(file.content).toContain('SUMMARY:Designers\\, Show\\; 2026');
    expect(file.content).toContain('LOCATION:Palace\\, Tashkent\\, Uzbekistan');
    expect(file.content).toContain('DTSTART;VALUE=DATE:20261125\r\nDTEND;VALUE=DATE:20261126');
    expect(file.content).toContain('SUMMARY:Application: Designers\\, Show\\; 2026');
  });

  it('writes an all-day span, and nothing for estimated or month dates', () => {
    const fair = eventModel('e2', {
      starts: { value: '2027-03-14', precision: 'day', certainty: 'announced' },
      ends: { value: '2027-03-16', precision: 'day', certainty: 'announced' },
    });
    const estimated = eventModel('e3', { starts: { value: '2027-03-14', precision: 'day', certainty: 'estimated' } });

    expect(createEventCalendar(fair, 'en', { now })!.content).toContain(
      'DTSTART;VALUE=DATE:20270314\r\nDTEND;VALUE=DATE:20270317',
    );
    expect(createEventCalendar(estimated, 'en', { now })).toBeNull();
    expect(
      createEventCalendar(
        eventModel('e4', { starts: { value: '2027-03', precision: 'month', certainty: 'announced' } }),
        'en',
      ),
    ).toBeNull();
  });
});

describe('date presets (UI05)', () => {
  it('turns each preset into the days the lists take, counted from today', () => {
    expect(eventDateRange('upcoming', '2026-11-10')).toEqual({ from: '2026-11-10', includeUndated: true });
    expect(eventDateRange('thisMonth', '2026-11-10')).toEqual({
      from: '2026-11-01',
      to: '2026-11-30',
      includeUndated: false,
    });
    expect(eventDateRange('thisMonth', '2028-02-10')).toEqual({
      from: '2028-02-01',
      to: '2028-02-29',
      includeUndated: false,
    });
    expect(eventDateRange('next3Months', '2026-11-10')).toEqual({
      from: '2026-11-10',
      to: '2027-02-10',
      includeUndated: false,
    });
    expect(eventDateRange('next3Months', '2026-11-30')).toEqual({
      from: '2026-11-30',
      to: '2027-02-28',
      includeUndated: false,
    });
    expect(eventDateRange('thisYear', '2026-11-10')).toEqual({
      from: '2026-01-01',
      to: '2026-12-31',
      includeUndated: false,
    });
    expect(eventDateRange('past', '2026-11-10')).toEqual({ to: '2026-11-09', includeUndated: false });
    expect(eventDateRange('past', '2026-03-01')).toEqual({ to: '2026-02-28', includeUndated: false });
  });

  it('takes the picked range for custom, and reads as upcoming until one is picked', () => {
    expect(eventDateRange('custom', '2026-11-10', { from: '2026-12-01', to: '2026-12-31' })).toEqual({
      from: '2026-12-01',
      to: '2026-12-31',
      includeUndated: false,
    });
    expect(eventDateRange('custom', '2026-11-10', null)).toEqual({ from: '2026-11-10', includeUndated: true });
    expect(addEventDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addEventDays('2026-11-10', 30)).toBe('2026-12-10');
  });
});
