import type {
  EventFacts,
  EventModel,
  EventSourceModel,
  TrackingModel,
  WorkflowStageModel,
} from '@wow-two-beta/fe-events/contracts';

/** Holds the day the sample cards read as "today", so each state looks the same every time the gallery opens. */
export const sampleToday = '2026-11-10';

/** Holds the sample workflow: the default stage and the stages the card's menu offers. */
export const sampleStages: readonly WorkflowStageModel[] = [
  { key: 'candidate', label: { en: 'Candidate' }, class: 'open' },
  { key: 'shortlisted', label: { en: 'Shortlisted' }, class: 'active' },
  { key: 'skipped', label: { en: 'Skipped' }, class: 'lost' },
];
export const sampleDefaultStage = 'candidate';
export const sampleQuickStages = ['shortlisted', 'skipped'] as const;
export const sampleKinds = [
  { key: 'runway', label: { en: 'Runway show' } },
  { key: 'exhibition', label: { en: 'Exhibition' } },
  { key: 'fair', label: { en: 'Fair' } },
  { key: 'workshop', label: { en: 'Workshop' } },
  { key: 'conference', label: { en: 'Conference' } },
];

/** Holds one sample card: the event and the organization's tracking of it, when it has any. */
export interface CardSample {
  readonly label: string;
  readonly event: EventModel;
  readonly tracking: TrackingModel | null;
}

const day = (value: string) => ({ value, precision: 'day', certainty: 'announced' }) as const;
const source = (id: string, name: string, state: EventSourceModel['state'] = 'live'): EventSourceModel => ({
  kind: 'scfd',
  id: `scfd/${id}`,
  name,
  state,
  firstSeenAtUtc: '2026-11-01T10:00:00Z',
  lastSeenAtUtc: '2026-11-09T10:00:00Z',
});

function event(
  id: string,
  facts: Partial<EventFacts>,
  sources: readonly EventSourceModel[] = [],
  posterUrl?: string,
): EventModel {
  return {
    id,
    catalog: 'ocharo',
    facts: {
      title: { und: id },
      kind: 'runway',
      status: 'scheduled',
      starts: { value: null, precision: 'unknown', certainty: 'announced' },
      ...(posterUrl ? { images: [{ url: posterUrl, attribution: 'Sample' }] } : {}),
      ...facts,
    },
    review: 'published',
    sources,
    pendingChanges: 0,
    revision: 1,
    createdAtUtc: '2026-11-01T10:00:00Z',
    updatedAtUtc: '2026-11-01T10:00:00Z',
  };
}

function tracked(eventId: string, stage: string, priority: TrackingModel['priority'] = 'normal'): TrackingModel {
  return {
    id: `track-${eventId}`,
    eventId,
    stage,
    priority,
    revision: 1,
    createdAtUtc: '2026-11-02T10:00:00Z',
    updatedAtUtc: '2026-11-02T10:00:00Z',
  };
}

/** Builds the sample cards: every state the card draws, with the three posters resolved by the caller. */
export function cardSamples(posters: { runway: string; expo: string; showcase: string }): readonly CardSample[] {
  return [
    {
      label: 'untracked, curated image, sources',
      event: event(
        'tashkent-fashion-week',
        {
          title: { en: 'Tashkent Fashion Week' },
          starts: day('2026-12-12'),
          ends: day('2026-12-14'),
          place: { city: 'Tashkent', country: 'UZ' },
        },
        [source('afisha', 'Afisha'), source('uzexpocentre', 'UzExpoCentre')],
        posters.runway,
      ),
      tracking: null,
    },
    {
      label: 'tracked, high priority, urgent deadline',
      event: event(
        'textile-expo',
        {
          title: { en: 'Central Asia Textile Expo' },
          kind: 'exhibition',
          starts: day('2027-03-03'),
          ends: day('2027-03-06'),
          place: { city: 'Almaty', country: 'KZ' },
          deadlines: [{ kind: 'application', at: day('2026-11-15') }],
        },
        [source('uzexpocentre', 'UzExpoCentre')],
        posters.expo,
      ),
      tracking: tracked('textile-expo', 'shortlisted', 'high'),
    },
    {
      label: 'date tile, untracked',
      event: event(
        'craft-fair',
        {
          title: { en: 'Samarkand Craft Fair' },
          kind: 'fair',
          starts: day('2027-03-14'),
          place: { city: 'Samarkand', country: 'UZ' },
          deadlines: [{ kind: 'registration', at: day('2027-01-20') }],
        },
        [source('craftfair', 'CraftFair')],
      ),
      tracking: null,
    },
    {
      label: 'cancelled (dimmed)',
      event: event(
        'silk-road-showcase',
        {
          title: { en: 'Silk Road Showcase' },
          status: 'cancelled',
          starts: day('2026-12-02'),
          place: { city: 'Bukhara', country: 'UZ' },
        },
        [source('afisha', 'Afisha')],
        posters.showcase,
      ),
      tracking: tracked('silk-road-showcase', 'candidate'),
    },
    {
      label: 'removed at source (dimmed)',
      event: event(
        'popup-market',
        {
          title: { en: 'Pop-up Market' },
          kind: 'fair',
          starts: day('2026-12-20'),
          place: { city: 'Tashkent', country: 'UZ' },
        },
        [source('anons', 'Anons', 'gone')],
      ),
      tracking: null,
    },
    {
      label: 'past (dimmed)',
      event: event(
        'spring-runway',
        {
          title: { en: 'Spring Runway' },
          starts: day('2026-10-18'),
          ends: day('2026-10-20'),
          place: { city: 'Tashkent', country: 'UZ' },
        },
        [source('afisha', 'Afisha')],
        posters.runway,
      ),
      tracking: tracked('spring-runway', 'skipped'),
    },
    {
      label: 'online, ongoing',
      event: event(
        'sustainable-masterclass',
        {
          title: { en: 'Sustainable Fashion Masterclass' },
          kind: 'workshop',
          attendance: 'online',
          starts: day('2026-11-09'),
          ends: day('2026-11-12'),
        },
        [source('anons', 'Anons')],
      ),
      tracking: tracked('sustainable-masterclass', 'candidate'),
    },
    {
      label: 'long title, month estimate',
      event: event(
        'long-title',
        {
          title: {
            en: 'The International Central Asia Fashion and Textile Week with Designers Showroom and Buyers Forum',
          },
          kind: 'conference',
          starts: { value: '2027-04', precision: 'month', certainty: 'estimated' },
          place: { city: 'Tashkent', country: 'UZ' },
        },
        [source('afisha', 'Afisha')],
      ),
      tracking: null,
    },
    {
      label: 'undated, icon alone',
      event: event('undated-exhibition', { title: { en: 'Someday Exhibition' }, kind: 'exhibition' }),
      tracking: null,
    },
    {
      label: 'postponed',
      event: event(
        'postponed-forum',
        {
          title: { en: 'Textile Industry Forum' },
          kind: 'conference',
          status: 'postponed',
          starts: day('2026-12-08'),
          place: { city: 'Namangan', country: 'UZ' },
        },
        [],
        posters.expo,
      ),
      tracking: null,
    },
  ];
}
