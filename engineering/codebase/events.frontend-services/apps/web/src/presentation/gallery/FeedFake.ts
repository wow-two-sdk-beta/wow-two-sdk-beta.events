import type {
  EventFacts,
  EventModel,
  EventSourceModel,
  TrackingFacetCount,
  TrackingFacetsModel,
  TrackingModel,
  TrackingSpec,
  TrackingSummaryModel,
  UpcomingDeadlineModel,
  WorkflowModel,
} from '@wow-two-beta/fe-events/contracts';
import { createEventsClient, type EventRegion, type EventsClient } from '@wow-two-beta/fe-events/kit';
import { sampleKinds, sampleToday } from './CardSamples';

/** Holds the feed page's workflow: the four stages of Ocharo's triage, `candidate` the default. */
export const feedWorkflow: WorkflowModel = {
  stages: [
    { key: 'candidate', label: { en: 'Candidate' }, class: 'open' },
    { key: 'shortlisted', label: { en: 'Shortlisted' }, class: 'active' },
    { key: 'skipped', label: { en: 'Skipped' }, class: 'lost' },
    { key: 'rejected', label: { en: 'Rejected' }, class: 'lost' },
  ],
  defaultStage: 'candidate',
  revision: 1,
};

/** Holds Ocharo's scope chips: Uzbekistan, its neighbours, and everything else. */
export const feedRegions: readonly EventRegion[] = [
  { key: 'local', label: { en: 'Local' }, countries: ['UZ'] },
  { key: 'regional', label: { en: 'Regional' }, countries: ['KZ', 'KG', 'TJ', 'TM', 'AF', 'AZ', 'RU'] },
  { key: 'international', label: { en: 'International' }, otherCountries: true },
];

const pad = (value: number) => String(value).padStart(2, '0');
const dayMs = 86_400_000;
const toDay = (ms: number) => {
  const date = new Date(ms);
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
};
const fromDay = (day: string) =>
  Date.UTC(Number(day.slice(0, 4)), Number(day.slice(5, 7)) - 1, Number(day.slice(8, 10)));
const shift = (day: string, days: number) => toDay(fromDay(day) + days * dayMs);
const announced = (value: string) => ({ value, precision: 'day', certainty: 'announced' }) as const;

const places = [
  { city: 'Tashkent', country: 'UZ' },
  { city: 'Samarkand', country: 'UZ' },
  { city: 'Bukhara', country: 'UZ' },
  { city: 'Almaty', country: 'KZ' },
  { city: 'Bishkek', country: 'KG' },
  { city: 'Moscow', country: 'RU' },
  { city: 'Paris', country: 'FR' },
  { city: 'Istanbul', country: 'TR' },
] as const;
const adjectives = [
  'Silk Road',
  'Central Asia',
  'Heritage',
  'Modern',
  'Atlas',
  'Golden',
  'Urban',
  'Craft',
  'Textile',
  'Spring',
];
const nouns = ['Fashion Week', 'Expo', 'Fair', 'Workshop', 'Forum', 'Showcase', 'Market', 'Summit'];
const sourceSets: readonly (readonly string[])[] = [[], ['Afisha'], ['UzExpoCentre'], ['Afisha', 'UzExpoCentre']];
/** Days from today to the application deadline of every fifth event: four fall inside 14 days, four beyond. */
const deadlineOffsets = [3, 6, 9, 12, 18, 25, 33, 41];

const source = (id: string, name: string): EventSourceModel => ({
  kind: 'scfd',
  id: `scfd/${id}`,
  name,
  state: 'live',
  firstSeenAtUtc: '2026-11-01T10:00:00Z',
  lastSeenAtUtc: '2026-11-09T10:00:00Z',
});

/** Holds the sample catalog: 40 events, 36 over Nov 2026 – Feb 2027, 2 undated and 2 past, with the tracking of some. */
export function feedCatalog(posters: readonly string[]): { events: EventModel[]; tracking: TrackingModel[] } {
  const events: EventModel[] = [];
  const tracking: TrackingModel[] = [];
  for (let i = 0; i < 40; i += 1) {
    const id = `ev-${pad(i + 1)}`;
    const place = places[(i * 3) % places.length]!;
    const names = sourceSets[i % sourceSets.length]!;
    const dated = i < 36;
    const month = Math.floor(i / 9);
    const start = dated
      ? month === 0
        ? `2026-11-${pad(12 + (i % 9) * 2)}`
        : `${month === 1 ? '2026-12' : month === 2 ? '2027-01' : '2027-02'}-${pad(3 + (i % 9) * 3)}`
      : i >= 38
        ? i === 38
          ? '2026-10-05'
          : '2026-10-18'
        : null;
    const deadlineOffset = i % 5 === 0 && i < 40 ? deadlineOffsets[(i / 5) % deadlineOffsets.length] : undefined;
    const poster = i % 6 === 1 || i % 6 === 3 || i % 6 === 5 ? posters[((i % 6) - 1) / 2] : undefined;
    const facts: EventFacts = {
      title: { en: `${adjectives[i % adjectives.length]} ${nouns[(i * 5) % nouns.length]} ${2026 + (i % 2)}` },
      kind: sampleKinds[i % sampleKinds.length]!.key,
      status: i === 7 ? 'cancelled' : i === 13 ? 'postponed' : 'scheduled',
      ...(i % 11 === 4 ? { attendance: 'online' as const } : {}),
      starts: start ? announced(start) : { value: null, precision: 'unknown', certainty: 'announced' },
      ...(start && i % 3 === 0 ? { ends: announced(shift(start, 2)) } : {}),
      place,
      organizer: { name: `Organizer ${pad(i + 1)}` },
      ...(deadlineOffset !== undefined
        ? { deadlines: [{ kind: 'application' as const, at: announced(shift(sampleToday, deadlineOffset)) }] }
        : {}),
      ...(poster ? { images: [{ url: poster, attribution: 'Sample' }] } : {}),
    };
    events.push({
      id,
      catalog: 'ocharo',
      facts,
      review: 'published',
      sources: names.map((name) => source(`${id}-${name.toLowerCase()}`, name)),
      pendingChanges: 0,
      revision: 1,
      createdAtUtc: `2026-11-${pad(1 + ((i * 7) % 9))}T${pad(8 + (i % 10))}:00:00Z`,
      updatedAtUtc: '2026-11-09T10:00:00Z',
    });
    const stage =
      i % 6 === 2
        ? 'shortlisted'
        : i % 9 === 4
          ? 'skipped'
          : i % 13 === 6
            ? 'rejected'
            : i % 10 === 3
              ? 'candidate'
              : null;
    if (stage) {
      tracking.push({
        id: `track-${id}`,
        eventId: id,
        stage,
        priority: i % 7 === 0 || i % 6 === 2 ? 'high' : 'normal',
        revision: 1,
        createdAtUtc: '2026-11-02T10:00:00Z',
        updatedAtUtc: '2026-11-02T10:00:00Z',
      });
    }
  }
  return { events, tracking };
}

interface Query {
  text: string;
  kinds: string[];
  countries: string[];
  excludeCountries: string[];
  cities: string[];
  sources: string[];
  stages: string[];
  excludeStages: string[];
  priority: string | null;
  from: string | null;
  to: string | null;
  includeUndated: boolean;
  deadlineBefore: string | null;
}

/** Reads the tracking list's query the way the client writes it: lists repeat their key. */
function parseQuery(params: URLSearchParams): Query {
  const all = (key: string, single?: string) => [
    ...params.getAll(key).map((value) => value.toLowerCase()),
    ...(single && params.get(single) ? [params.get(single)!.toLowerCase()] : []),
  ];
  return {
    text: (params.get('text') ?? '').toLowerCase(),
    kinds: all('kinds', 'kind'),
    countries: [...params.getAll('countries'), ...(params.get('country') ? [params.get('country')!] : [])].map(
      (value) => value.toUpperCase(),
    ),
    excludeCountries: params.getAll('excludeCountries').map((value) => value.toUpperCase()),
    cities: all('cities', 'city'),
    sources: all('sources'),
    stages: all('stages', 'stage'),
    excludeStages: all('excludeStages'),
    priority: params.get('priority'),
    from: params.get('from'),
    to: params.get('to'),
    includeUndated: params.get('includeUndated') === 'true',
    deadlineBefore: params.get('deadlineBefore'),
  };
}

type Facet = 'countries' | 'cities' | 'kinds' | 'stages' | 'priorities' | 'sources' | 'deadline';

const startOf = (event: EventModel) => event.facts.starts?.value?.slice(0, 10) ?? null;
const endOf = (event: EventModel) => event.facts.ends?.value?.slice(0, 10) ?? startOf(event);
const openDeadline = (event: EventModel, today: string) =>
  (event.facts.deadlines ?? [])
    .filter((item) => item.kind === 'application' && item.at.value && item.at.value.slice(0, 10) >= today)
    .map((item) => item.at.value!.slice(0, 10))
    .sort()[0] ?? null;
const sourceKeys = (event: EventModel) =>
  event.sources?.length ? event.sources.map((item) => (item.name ?? '').toLowerCase()) : ['curated'];

/** Creates the in-memory service behind the feed page: the real client over a fetch that answers every tracking route. */
export function createFeedFake(options: { posters?: readonly string[]; latencyMs?: number } = {}) {
  const { events, tracking: seeded } = feedCatalog(options.posters ?? []);
  const store = new Map(events.map((event) => [event.id, event]));
  const tracked = new Map(seeded.map((item) => [item.eventId, item]));
  const today = sampleToday;
  const defaultStage = feedWorkflow.defaultStage;
  const stageOf = (event: EventModel) => tracked.get(event.id)?.stage ?? defaultStage;

  /** Says whether an event matches the query, leaving out the selection of the facet being counted. */
  function matches(event: EventModel, query: Query, skip: Facet | null): boolean {
    const place = event.facts.place;
    const start = startOf(event);
    if (query.text) {
      const haystack = [event.facts.title.en, place?.city, event.facts.organizer?.name].join(' ').toLowerCase();
      if (!haystack.includes(query.text)) return false;
    }
    if (skip !== 'kinds' && query.kinds.length && !query.kinds.includes((event.facts.kind ?? '').toLowerCase()))
      return false;
    if (skip !== 'countries') {
      const country = place?.country?.toUpperCase();
      if (query.countries.length && (!country || !query.countries.includes(country))) return false;
      if (query.excludeCountries.length && country && query.excludeCountries.includes(country)) return false;
    }
    if (skip !== 'cities' && query.cities.length && !query.cities.includes((place?.city ?? '').toLowerCase()))
      return false;
    if (skip !== 'sources' && query.sources.length && !sourceKeys(event).some((key) => query.sources.includes(key)))
      return false;
    if (skip !== 'stages') {
      const stage = stageOf(event);
      if (query.stages.length ? !query.stages.includes(stage) : query.excludeStages.includes(stage)) return false;
    }
    if (skip !== 'priorities' && query.priority && tracked.get(event.id)?.priority !== query.priority) return false;
    if (skip !== 'deadline' && query.deadlineBefore) {
      const next = openDeadline(event, today);
      if (!next || next > query.deadlineBefore) return false;
    }
    if (start === null) return query.includeUndated;
    if (query.from && (endOf(event) ?? start) < query.from) return false;
    if (query.to && start > query.to) return false;
    return true;
  }

  const rows = (query: Query, skip: Facet | null) => [...store.values()].filter((event) => matches(event, query, skip));

  function counted(values: string[], order?: readonly string[]): TrackingFacetCount[] {
    const counts = new Map<string, number>(order?.map((key) => [key, 0]));
    for (const value of values) counts.set(value, (counts.get(value) ?? 0) + 1);
    const list = [...counts.entries()].map(([value, count]) => ({ value, count }));
    return order ? list : list.sort((a, b) => b.count - a.count || a.value.localeCompare(b.value));
  }

  function facets(query: Query): TrackingFacetsModel {
    const sourcesOf = rows(query, 'sources');
    const names = new Map<string, string>();
    for (const event of sourcesOf)
      for (const item of event.sources ?? []) names.set((item.name ?? '').toLowerCase(), item.name ?? '');
    const sourceCounts = counted(sourcesOf.flatMap((event) => [...new Set(sourceKeys(event))]));
    if (!sourceCounts.some((item) => item.value === 'curated')) sourceCounts.push({ value: 'curated', count: 0 });
    const before = query.deadlineBefore ?? shift(today, 30);
    return {
      total: rows(query, null).length,
      countries: counted(
        rows(query, 'countries').flatMap((event) => (event.facts.place?.country ? [event.facts.place.country] : [])),
      ),
      cities: counted(
        rows(query, 'cities').flatMap((event) =>
          event.facts.place?.city ? [event.facts.place.city.toLowerCase()] : [],
        ),
      ),
      kinds: counted(
        rows(query, 'kinds').flatMap((event) => (event.facts.kind ? [event.facts.kind] : [])),
        sampleKinds.map((item) => item.key),
      ),
      stages: counted(
        rows(query, 'stages').map(stageOf),
        feedWorkflow.stages.map((item) => item.key),
      ),
      priorities: counted(
        rows(query, 'priorities').flatMap((event) => (tracked.has(event.id) ? [tracked.get(event.id)!.priority] : [])),
        ['high', 'normal', 'low'],
      ),
      sources: sourceCounts.map((item) => (names.has(item.value) ? { ...item, label: names.get(item.value)! } : item)),
      openDeadline: {
        before,
        count: rows(query, 'deadline').filter((event) => {
          const next = openDeadline(event, today);
          return next !== null && next <= before;
        }).length,
      },
    };
  }

  function summary(): TrackingSummaryModel {
    const byStage: Record<string, number> = {};
    for (const item of tracked.values()) byStage[item.stage] = (byStage[item.stage] ?? 0) + 1;
    const upcomingDeadlines: UpcomingDeadlineModel[] = [...store.values()]
      .filter((event) => event.facts.status !== 'cancelled' && openDeadline(event, today))
      .sort((a, b) => openDeadline(a, today)!.localeCompare(openDeadline(b, today)!))
      .map((event) => ({
        eventId: event.id,
        title: event.facts.title,
        deadline: { kind: 'application' as const, at: announced(openDeadline(event, today)!) },
        stage: stageOf(event),
        tracked: tracked.has(event.id),
      }));
    return { byStage, untracked: store.size - tracked.size, upcomingDeadlines };
  }

  function ordered(list: EventModel[], sort: string | null): EventModel[] {
    const byId = (a: EventModel, b: EventModel) => a.id.localeCompare(b.id);
    if (sort === 'added') return [...list].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc) || byId(a, b));
    if (sort === 'deadline') {
      return [...list].sort((a, b) => {
        const left = openDeadline(a, today);
        const right = openDeadline(b, today);
        if (left && right) return left.localeCompare(right) || byId(a, b);
        return left ? -1 : right ? 1 : byId(a, b);
      });
    }
    return [...list].sort((a, b) => {
      const left = startOf(a);
      const right = startOf(b);
      if (left && right) return left.localeCompare(right) || byId(a, b);
      return left ? -1 : right ? 1 : byId(a, b);
    });
  }

  const answer = (data: unknown, status = 200) => Response.json({ data }, { status });

  async function respond(request: Request): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname;
    if (path.endsWith('/workflow')) return answer(feedWorkflow);
    if (path.endsWith('/tracking/summary')) return answer(summary());
    const history = /\/tracking\/([^/]+)\/history$/.exec(path);
    if (request.method === 'GET' && history) return answer([]);
    if (path.endsWith('/tracking/facets')) return answer(facets(parseQuery(url.searchParams)));
    if (request.method === 'POST' && path.endsWith('/candidates')) {
      const spec = (await request.json()) as { facts: EventFacts };
      const created: EventModel = {
        id: `ev-${pad(store.size + 1)}`,
        catalog: 'ocharo',
        facts: spec.facts,
        review: 'published',
        sources: [],
        pendingChanges: 0,
        revision: 1,
        createdAtUtc: `${today}T12:00:00Z`,
        updatedAtUtc: `${today}T12:00:00Z`,
      };
      store.set(created.id, created);
      return answer(created, 201);
    }
    const write = /\/tracking\/([^/]+)$/.exec(path);
    if (request.method === 'PUT' && write) {
      const eventId = decodeURIComponent(write[1]!);
      const spec = (await request.json()) as TrackingSpec;
      const current = tracked.get(eventId);
      const next: TrackingModel = {
        id: current?.id ?? `track-${eventId}`,
        eventId,
        stage: spec.stage ?? defaultStage,
        priority: spec.priority ?? current?.priority ?? 'normal',
        revision: (current?.revision ?? 0) + 1,
        createdAtUtc: current?.createdAtUtc ?? `${today}T12:00:00Z`,
        updatedAtUtc: `${today}T12:00:00Z`,
      };
      tracked.set(eventId, next);
      return answer(next);
    }
    const read = /\/tracking\/([^/]+)$/.exec(path);
    if (request.method === 'GET' && read) {
      const eventId = decodeURIComponent(read[1]!);
      const event = store.get(eventId);
      return event
        ? answer({ event, tracking: tracked.get(eventId) ?? null })
        : Response.json(
            { title: 'Not found' },
            { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
          );
    }
    if (request.method === 'GET' && path.endsWith('/tracking')) {
      const query = parseQuery(url.searchParams);
      const limit = Number(url.searchParams.get('limit') ?? 12) || 12;
      const cursor = url.searchParams.get('cursor');
      const offset = cursor && /^c\d+$/.test(cursor) ? Number(cursor.slice(1)) : 0;
      const list = ordered(rows(query, null), url.searchParams.get('sort'));
      const page = list.slice(offset, offset + limit);
      return answer({
        items: page.map((event) => ({ event, tracking: tracked.get(event.id) ?? null })),
        nextCursor: offset + limit < list.length ? `c${offset + limit}` : null,
      });
    }
    return Response.json(
      { title: 'Not found' },
      { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
    );
  }

  /** Answers like the service, after `latencyMs`; an aborted request rejects as a browser's fetch does. */
  const fetch: typeof globalThis.fetch = (input, init) =>
    new Promise<Response>((resolve, reject) => {
      const request = new Request(input, init);
      const aborted = () => reject(new DOMException('The request was aborted.', 'AbortError'));
      if (request.signal.aborted) return aborted();
      const timer = setTimeout(() => {
        request.signal.removeEventListener('abort', onAbort);
        respond(request).then(resolve, reject);
      }, options.latencyMs ?? 120);
      function onAbort() {
        clearTimeout(timer);
        aborted();
      }
      request.signal.addEventListener('abort', onAbort, { once: true });
    });

  const client: EventsClient = createEventsClient({ baseUrl: 'https://events.fake.test', fetch });
  return { client, fetch, events: [...store.values()] };
}
