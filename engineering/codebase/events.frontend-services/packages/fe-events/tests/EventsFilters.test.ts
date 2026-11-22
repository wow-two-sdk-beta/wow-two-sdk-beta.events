import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { h } from 'vue';
import type { TrackedEventModel, WorkflowModel } from '../src/contracts';
import {
  eventDateRange,
  EventDatePresetValues,
  EventsFacetKind,
  EventsFacetPlacement,
  EventsFeed,
  eventsFilterLabels,
  useEventsFilters,
  type EventRegion,
  type EventsClient,
  type EventsFacet,
  type EventsFilters,
  type EventsFiltersOptions,
} from '../src/kit';
import { envelope, eventModel, facetCount, facetsModel, fakeService, stubIntersectionObserver } from './Fixtures';
import { filterBar, Host } from './FeedPage';
import { pick } from './Picker';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

const workflow: WorkflowModel = {
  stages: [
    { key: 'candidate', label: { en: 'Candidate' }, class: 'open' },
    { key: 'shortlisted', label: { en: 'Shortlisted' }, class: 'active' },
    { key: 'skipped', label: { en: 'Skipped' }, class: 'lost' },
    { key: 'rejected', label: { en: 'Rejected' }, class: 'lost' },
  ],
  defaultStage: 'candidate',
  revision: 1,
};
const regions: EventRegion[] = [
  { key: 'local', label: { en: 'Local' }, countries: ['UZ'] },
  { key: 'regional', label: { en: 'Regional' }, countries: ['KZ', 'RU'] },
  { key: 'international', label: { en: 'International' }, otherCountries: true },
];
const kinds = [
  { key: 'runway', label: { en: 'Runway' } },
  { key: 'exhibition', label: { en: 'Expo' } },
];
const ocharo = { hiddenStages: ['skipped', 'rejected'], regions, kinds };
const imported = eventModel('imported', {
  title: { en: 'Textile Expo' },
  starts: { value: '2026-12-12', precision: 'day', certainty: 'announced' },
  place: { city: 'Tashkent', country: 'UZ' },
});
const items: TrackedEventModel[] = [{ event: imported, tracking: null }];

/** Answers each route of the tracking feed. */
function router(list: TrackedEventModel[] = items) {
  return (request: Request) => {
    const path = new URL(request.url).pathname;
    if (path.endsWith('/workflow')) return envelope(workflow);
    if (path.endsWith('/tracking/summary')) {
      return envelope({ byStage: {}, untracked: 0, upcomingDeadlines: [] });
    }
    if (path.endsWith('/tracking/facets')) {
      return envelope(
        facetsModel(72, {
          countries: [facetCount('UZ', 40), facetCount('KZ', 10), facetCount('RU', 5), facetCount('FR', 3)],
        }),
      );
    }
    return envelope({ items: list, nextCursor: null });
  };
}

const paths = (requests: Request[], suffix: string) =>
  requests.filter((request) => request.method === 'GET' && new URL(request.url).pathname.endsWith(suffix));
const listRequests = (requests: Request[]) => paths(requests, '/tracking');
const facetRequests = (requests: Request[]) => paths(requests, '/tracking/facets');
const lastParams = (requests: Request[]) => new URL(requests.at(-1)!.url).searchParams;
const pause = (ms = 300) => new Promise((resolve) => setTimeout(resolve, ms));

/** Calls `useEventsFilters` in a component's setup, as a page does, and gives back its state. */
async function stateOf(client: EventsClient, extra: Partial<EventsFiltersOptions> = {}) {
  let filters!: EventsFilters;
  const wrapper = mount(Host, {
    props: {
      build: () => {
        filters = useEventsFilters({ client, catalog: 'ocharo', today: '2026-11-10', tracking: true, ...extra });
        return () => h('div');
      },
    },
  });
  wrappers.push(wrapper);
  await flushPromises();
  return filters;
}

interface PageOptions {
  actionsTo?: string;
  extra?: Partial<EventsFiltersOptions>;
}

/**
 * A page that builds the filter bar in a card of its own: one `useEventsFilters` feeds the page's bar (`filterBar`, made
 * of the neutral facets alone) and an `EventsFeed`; the card has an element for the feed's actions.
 */
async function mountPage(responder: (request: Request) => Response, options: PageOptions = {}) {
  const service = fakeService(responder);
  let filters!: EventsFilters;
  const wrapper = mount(Host, {
    props: {
      build: () => {
        filters = useEventsFilters({
          client: service.client,
          catalog: 'ocharo',
          today: '2026-11-10',
          tracking: true,
          ...ocharo,
          ...options.extra,
        });
        return () =>
          h('div', [
            h('div', { id: 'filters-card' }, [filterBar(filters), h('div', { id: 'card-actions' })]),
            h(EventsFeed, {
              client: service.client,
              catalog: 'ocharo',
              today: '2026-11-10',
              tracking: true,
              canPropose: true,
              ...ocharo,
              filters,
              ...(options.actionsTo ? { actionsTo: options.actionsTo } : {}),
            }),
          ]);
      },
    },
    attachTo: document.body,
  });
  wrappers.push(wrapper);
  await flushPromises();
  return { ...service, wrapper, filters, feed: wrapper.findComponent(EventsFeed) };
}

describe('shared filter state (T11)', () => {
  it("renders no bar of its own: the page's bar is the only search box, and the feed keeps its actions row", async () => {
    const { wrapper, feed } = await mountPage(router());

    expect(feed.find('[data-test="events-toolbar"]').exists()).toBe(false);
    expect(feed.find('input[type="search"]').exists()).toBe(false);
    expect(wrapper.findAll('input[type="search"]')).toHaveLength(1);
    expect(wrapper.get('#filters-card').find('input[type="search"]').exists()).toBe(true);
    expect(feed.find('[data-test="events-actions"]').exists()).toBe(true);
  });

  it("lets a page's own bar drive the feed's list: a stage, a city, a source and a search all reach it", async () => {
    const { wrapper, requests, filters, feed } = await mountPage(router());

    const actions = feed.get('[data-test="events-actions"]');
    expect(actions.find('[data-test="feed-sort"]').exists()).toBe(true);
    expect(actions.findAll('button').some((button) => button.text() === 'Propose an event')).toBe(true);
    expect(lastParams(listRequests(requests)).getAll('excludeStages')).toEqual(['skipped', 'rejected']);

    filters.selection.value = { stage: ['shortlisted'], city: ['tashkent'], source: ['afisha'] };
    await flushPromises();
    await wrapper.get('input[type="search"]').setValue('fashion');
    await pause();
    await flushPromises();

    const params = lastParams(listRequests(requests));
    expect(params.getAll('stages')).toEqual(['shortlisted']);
    expect(params.getAll('cities')).toEqual(['tashkent']);
    expect(params.getAll('sources')).toEqual(['afisha']);
    expect(params.get('text')).toBe('fashion');
    expect(params.has('excludeStages')).toBe(false);
    expect(filters.filtered.value).toBe(true);

    await pick(feed.get('[data-test="feed-sort"]').element, 'Recently added');
    await flushPromises();

    expect(filters.sort.value).toBe('added');
    expect(lastParams(listRequests(requests)).get('sort')).toBe('added');
  });

  it("reads the counts and the workflow once, through the page's state: the feed starts no facet requests of its own", async () => {
    const { requests } = await mountPage(router());

    expect(facetRequests(requests)).toHaveLength(2);
    expect(paths(requests, '/workflow')).toHaveLength(1);
  });

  it("shows the match pill from the page's own counts", async () => {
    const { wrapper } = await mountPage(router());

    expect(wrapper.get('[data-test="page-bar-match"]').text()).toBe('72 of 72');
  });

  it("moves the feed's actions row into the page's card with `actionsTo`", async () => {
    const inline = await mountPage(router());
    expect(inline.feed.find('[data-test="events-actions"]').exists()).toBe(true);
    expect(document.querySelector('#card-actions [data-test="events-actions"]')).toBeNull();
    inline.wrapper.unmount();
    wrappers.splice(wrappers.indexOf(inline.wrapper), 1);
    document.body.innerHTML = '';

    const moved = await mountPage(router(), { actionsTo: '#card-actions' });

    const row = document.querySelector('#card-actions [data-test="events-actions"]');
    expect(row).not.toBeNull();
    expect(row!.querySelector('[data-test="feed-sort"]')).not.toBeNull();
    expect(
      [...row!.querySelectorAll('button')].some((button) => button.textContent?.trim() === 'Propose an event'),
    ).toBe(true);
    expect(moved.feed.find('[data-test="events-actions"]').exists()).toBe(false);
  });

  it("gives the page neutral facets with the server's counts under `tracking`: keys, kinds, placements and counts", async () => {
    const { filters } = await mountPage(router());

    const facets = filters.facetDefinitions.value;
    expect(facets.map((item) => `${item.key}:${item.kind}:${item.placement}`)).toEqual([
      'scope:single:inline',
      'kind:multi:inline',
      'stage:multi:popover',
      'hidden:toggle:popover',
      'city:multi:popover',
      'source:multi:popover',
      'priority:toggle:popover',
      'deadline:toggle:popover',
      'when:single:popover',
    ]);
    const countsOf = (key: string) => {
      const facet = facets.find((item) => item.key === key);
      return facet && facet.kind !== 'toggle' ? facet.options.map((option) => option.count) : [];
    };
    expect(countsOf('scope')).toEqual([undefined, 40, 15, 3]);
    expect(countsOf('kind')).toEqual([2, 0]);
    expect(countsOf('stage')).toEqual([2, 1, 2, 0]);
    expect(facets.find((item) => item.key === 'priority')).toEqual({
      key: 'priority',
      label: 'High priority',
      kind: 'toggle',
      placement: 'popover',
      count: 1,
    });
    expect(facets.find((item) => item.key === 'deadline')).toMatchObject({ kind: 'toggle', count: 1 });
    // A choice facet is plain data: the key, the label, the kind, the placement and options of value, label and count.
    expect(facets.find((item) => item.key === 'city')).toEqual({
      key: 'city',
      label: 'City',
      kind: 'multi',
      placement: 'popover',
      options: [
        { value: 'tashkent', label: 'Tashkent', count: 1 },
        { value: 'almaty', label: 'Almaty', count: 1 },
      ],
    });
    expect(filters.matchCount.value).toBe(72);
    expect(filters.totalCount.value).toBe(72);
  });

  it('names the facet kinds and placements the way the facets carry them', () => {
    expect(EventsFacetKind).toEqual({ Single: 'single', Multi: 'multi', Toggle: 'toggle' });
    expect(EventsFacetPlacement).toEqual({ Inline: 'inline', Popover: 'popover' });
    const facet: EventsFacet = {
      key: 'kind',
      label: 'Kind',
      kind: EventsFacetKind.Single,
      placement: EventsFacetPlacement.Inline,
      options: [{ value: 'runway', label: 'Runway' }],
    };
    expect(facet.kind).toBe('single');
  });

  it("lets a page's bar write a pick and a toggle through the neutral facets", async () => {
    const { wrapper, requests } = await mountPage(router());

    await wrapper.get('[data-facet="kind"] [data-value="runway"]').trigger('click');
    await flushPromises();
    expect(lastParams(listRequests(requests)).getAll('kinds')).toEqual(['runway']);

    await wrapper.get('[data-facet="priority"] button').trigger('click');
    await flushPromises();
    expect(lastParams(listRequests(requests)).get('priority')).toBe('high');
    expect(wrapper.get('[data-facet="priority"] button').attributes('aria-pressed')).toBe('true');

    await wrapper.get('[data-facet="priority"] button').trigger('click');
    await flushPromises();
    expect(lastParams(listRequests(requests)).get('priority')).toBeNull();
  });

  it('gives the page the strings its bar needs, in each language, with the placeholders left to fill', () => {
    const keys = [
      'searchLabel',
      'searchPlaceholder',
      'filtersLabel',
      'clearAllLabel',
      'anyLabel',
      'selectedLabel',
      'removeLabel',
      'removeToggleLabel',
      'matchLabel',
    ];
    for (const locale of ['en', 'ru', 'uz'] as const) {
      const labels = eventsFilterLabels(locale);

      expect(Object.keys(labels), locale).toEqual(keys);
      expect(
        Object.values(labels).every((value) => value !== ''),
        locale,
      ).toBe(true);
      expect(labels.matchLabel, locale).toContain('{matched}');
      expect(labels.matchLabel, locale).toContain('{total}');
      expect(labels.selectedLabel, locale).toContain('{count}');
      expect(labels.removeLabel, locale).toContain('{facet}');
      expect(labels.removeLabel, locale).toContain('{value}');
      expect(labels.removeToggleLabel, locale).toContain('{facet}');
    }
    expect(eventsFilterLabels()).toEqual({
      searchLabel: 'Search events',
      searchPlaceholder: eventsFilterLabels('en').searchPlaceholder,
      filtersLabel: 'Filters',
      clearAllLabel: 'Clear all',
      anyLabel: 'Any',
      selectedLabel: '{count} selected',
      removeLabel: 'Remove {facet}: {value}',
      removeToggleLabel: 'Remove {facet}',
      matchLabel: '{matched} of {total}',
    });
    expect(eventsFilterLabels('ru').removeToggleLabel).toBe('Убрать: {facet}');
  });

  it('builds the calendar window from every picked filter and ignores the date preset', async () => {
    const service = fakeService(router());
    const filters = await stateOf(service.client, ocharo);
    filters.select({ stage: ['shortlisted'], city: ['tashkent'], source: ['afisha'], when: 'thisYear' });
    filters.search.value = 'fashion';
    await pause();

    expect(filters.preset.value).toBe('thisYear');
    expect(filters.toWindowQuery('2026-12-01', '2026-12-31')).toEqual({
      text: 'fashion',
      stages: ['shortlisted'],
      cities: ['tashkent'],
      sources: ['afisha'],
      from: '2026-12-01',
      to: '2026-12-31',
    });

    filters.select({});
    filters.search.value = '';
    await pause();

    expect(filters.preset.value).toBe('upcoming');
    expect(filters.toWindowQuery('2026-12-01', '2026-12-31')).toEqual({
      excludeStages: ['skipped', 'rejected'],
      from: '2026-12-01',
      to: '2026-12-31',
    });
    // The feed's own query still follows the preset.
    expect(filters.trackingQuery.value).toEqual({
      includeUndated: true,
      from: '2026-11-10',
      excludeStages: ['skipped', 'rejected'],
    });
  });

  it('passes a month-precision event through the window with its precision unchanged', async () => {
    const december = eventModel('december', {
      title: { en: 'Winter Market' },
      starts: { value: '2026-12', precision: 'month', certainty: 'estimated' },
    });
    const service = fakeService(router([{ event: december, tracking: null }]));
    const filters = await stateOf(service.client, ocharo);

    const window = filters.toWindowQuery('2026-12-01', '2026-12-31');
    const result = await service.client.listTracking('ocharo', window);

    const params = lastParams(listRequests(service.requests));
    expect(params.get('from')).toBe('2026-12-01');
    expect(params.get('to')).toBe('2026-12-31');
    expect(params.has('includeUndated')).toBe(false);
    expect(result.ok).toBe(true);
    const starts = result.ok ? result.value.items[0]!.event.facts.starts : null;
    expect(starts).toEqual({ value: '2026-12', precision: 'month', certainty: 'estimated' });
    expect(starts?.precision).toBe('month');
  });

  it('restores the same state from the URL query it wrote', async () => {
    const service = fakeService(router());
    const first = await stateOf(service.client, ocharo);
    expect(first.toRouteQuery()).toEqual({});
    first.select({
      stage: ['shortlisted', 'candidate'],
      kind: ['runway'],
      city: ['tashkent'],
      source: ['afisha'],
      scope: 'regional',
      when: 'thisMonth',
    });
    first.search.value = ' fashion week ';
    first.sort.value = 'added';
    await pause();

    const query = first.toRouteQuery();

    expect(query).toEqual({
      q: 'fashion week',
      stages: ['shortlisted', 'candidate'],
      kinds: ['runway'],
      cities: ['tashkent'],
      sources: ['afisha'],
      regions: 'regional',
      when: 'thisMonth',
      sort: 'added',
    });
    // A router writes the query as text and reads one value as a string, several as an array.
    const written = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      for (const item of Array.isArray(value) ? value : [value]) written.append(key, item);
    }
    const read: Record<string, string | string[]> = {};
    for (const key of new Set(written.keys())) {
      const all = written.getAll(key);
      read[key] = all.length === 1 ? all[0]! : all;
    }
    const second = await stateOf(service.client, ocharo);

    second.fromRouteQuery(read);

    expect(second.selection.value).toEqual(first.selection.value);
    expect(second.search.value).toBe('fashion week');
    expect(second.sort.value).toBe('added');
    expect(second.trackingQuery.value).toEqual(first.trackingQuery.value);
    expect(second.toRouteQuery()).toEqual(query);
    expect(second.queryKey.value).toBe(first.queryKey.value);

    second.fromRouteQuery({});

    expect(second.selection.value).toEqual({});
    expect(second.search.value).toBe('');
    expect(second.sort.value).toBe('starts');
    expect(second.toRouteQuery()).toEqual({});
  });

  it('keeps the toggles and the custom range a URL echo does not carry', async () => {
    const service = fakeService(router());
    const filters = await stateOf(service.client, ocharo);
    filters.select({ priority: true, when: 'custom', stage: ['shortlisted'] });
    filters.customRange.value = { from: '2026-12-01', to: '2026-12-05' };

    filters.fromRouteQuery(filters.toRouteQuery());

    expect(filters.selection.value).toEqual({ priority: true, when: 'custom', stage: ['shortlisted'] });
    expect(filters.customRange.value).toEqual({ from: '2026-12-01', to: '2026-12-05' });
    expect(filters.trackingQuery.value).toMatchObject({ priority: 'high', from: '2026-12-01', to: '2026-12-05' });
  });

  it('writes a pick through `selection` the way the bar reports it, leaving the defaults out', async () => {
    const service = fakeService(router());
    const filters = await stateOf(service.client, ocharo);

    filters.selection.value = { scope: 'all', when: 'upcoming', city: ['tashkent'] };

    expect(filters.selection.value).toEqual({ city: ['tashkent'] });
    expect(filters.toRouteQuery()).toEqual({ cities: ['tashkent'] });
  });
});

describe('the today preset (T11)', () => {
  it('lists one day, with undated events left out', () => {
    expect(eventDateRange('today', '2026-11-22')).toEqual({
      from: '2026-11-22',
      to: '2026-11-22',
      includeUndated: false,
    });
    expect(EventDatePresetValues.slice(0, 3)).toEqual(['upcoming', 'today', 'thisMonth']);
  });

  it('shows in the `when` facet, and a pick lists that day through the feed', async () => {
    const { filters, requests } = await mountPage(router());
    const when = filters.facetDefinitions.value.find((item) => item.key === 'when');
    expect(when && when.kind !== 'toggle' ? when.options.map((option) => [option.value, option.label]) : []).toEqual([
      ['upcoming', 'Upcoming'],
      ['today', 'Today'],
      ['thisMonth', 'This month'],
      ['next3Months', 'Next 3 months'],
      ['thisYear', 'This year'],
      ['past', 'Past'],
      ['custom', 'Custom range…'],
    ]);

    filters.selection.value = { when: 'today' };
    await flushPromises();

    const params = lastParams(listRequests(requests));
    expect(params.get('from')).toBe('2026-11-10');
    expect(params.get('to')).toBe('2026-11-10');
    expect(params.has('includeUndated')).toBe(false);
  });

  it('is labelled in each language', async () => {
    const service = fakeService(router());
    const read = async (locale: 'en' | 'ru' | 'uz') => {
      const filters = await stateOf(service.client, { ...ocharo, locale });
      const when = filters.facetDefinitions.value.find((item) => item.key === 'when');
      return when && when.kind !== 'toggle'
        ? when.options.find((option) => option.value === 'today')?.label
        : undefined;
    };

    expect([await read('en'), await read('ru'), await read('uz')]).toEqual(['Today', 'Сегодня', 'Bugun']);
  });
});

describe('scrolling with the page-level state (T11)', () => {
  it('loads the next cursor when the sentinel scrolls into view', async () => {
    const view = stubIntersectionObserver();
    const { requests } = await mountPage((request) => {
      const url = new URL(request.url);
      if (url.pathname.endsWith('/tracking') && request.method === 'GET') {
        return envelope({ items, nextCursor: url.searchParams.get('cursor') ? null : 'c2' });
      }
      return router()(request);
    });

    expect(listRequests(requests)).toHaveLength(1);
    view.fire();
    await flushPromises();

    expect(listRequests(requests)).toHaveLength(2);
    expect(lastParams(listRequests(requests)).get('cursor')).toBe('c2');
  });
});
