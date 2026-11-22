import { flushPromises, mount } from '@vue/test-utils';
import { FacetBar, type FacetDefinition } from '@wow-two-beta/fe/presentation/forms';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { defineComponent, h, type PropType, type VNodeChild } from 'vue';
import type { TrackedEventModel, WorkflowModel } from '../src/contracts';
import {
  eventDateRange,
  EventDatePresetValues,
  EventsFeed,
  eventsFacetBarLabels,
  useEventsFilters,
  type EventRegion,
  type EventsClient,
  type EventsFilters,
  type EventsFiltersOptions,
} from '../src/kit';
import { envelope, eventModel, facetCount, facetsModel, fakeService, stubIntersectionObserver } from './Fixtures';

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

/** A component whose setup runs `build`, so a page's setup can be written in a test and its lifecycle hooks attach. */
const Host = defineComponent({
  props: { build: { type: Function as PropType<() => () => VNodeChild>, required: true } },
  setup: (props) => props.build(),
});

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
 * A page that hosts the filter bar in a card of its own: one `useEventsFilters` feeds a `FacetBar` and an `EventsFeed`;
 * the card has an element for the feed's actions.
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
            h('div', { id: 'filters-card' }, [
              h(FacetBar, {
                facets: filters.facetDefinitions.value,
                modelValue: filters.selection.value,
                search: filters.search.value,
                matchCount: filters.matchCount.value,
                totalCount: filters.totalCount.value,
                ...eventsFacetBarLabels('en'),
                'onUpdate:modelValue': (next: Parameters<typeof filters.select>[0]) => (filters.selection.value = next),
                'onUpdate:search': (value: string) => (filters.search.value = value),
              }),
              h('div', { id: 'card-actions' }),
            ]),
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
  it('keeps the feed with no `filters` as it was: its own toolbar, no actions row', async () => {
    const service = fakeService(router());
    const wrapper = mount(EventsFeed, {
      props: { client: service.client, catalog: 'ocharo', today: '2026-11-10', tracking: true, canPropose: true },
    });
    wrappers.push(wrapper);
    await flushPromises();

    const toolbar = wrapper.get('[data-test="events-toolbar"]');
    expect(toolbar.findComponent(FacetBar).exists()).toBe(true);
    expect(toolbar.find('select[name="sort"]').exists()).toBe(true);
    expect(toolbar.findAll('button').some((button) => button.text() === 'Propose an event')).toBe(true);
    expect(wrapper.find('[data-test="events-actions"]').exists()).toBe(false);
  });

  it("lets a page's FacetBar drive the feed's list: a stage, a city, a source and a search all reach it", async () => {
    const { wrapper, requests, filters, feed } = await mountPage(router());

    expect(feed.find('[data-test="events-toolbar"]').exists()).toBe(false);
    expect(feed.findComponent(FacetBar).exists()).toBe(false);
    const actions = feed.get('[data-test="events-actions"]');
    expect(actions.find('select[name="sort"]').exists()).toBe(true);
    expect(actions.findAll('button').some((button) => button.text() === 'Propose an event')).toBe(true);
    expect(lastParams(listRequests(requests)).getAll('excludeStages')).toEqual(['skipped', 'rejected']);

    const bar = wrapper.findComponent(FacetBar);
    bar.vm.$emit('update:modelValue', { stage: ['shortlisted'], city: ['tashkent'], source: ['afisha'] });
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

    await feed.get('select[name="sort"]').setValue('added');
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

    expect(wrapper.get('[data-facet-match]').text()).toBe('72 of 72');
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
    expect(row!.querySelector('select[name="sort"]')).not.toBeNull();
    expect(
      [...row!.querySelectorAll('button')].some((button) => button.textContent?.trim() === 'Propose an event'),
    ).toBe(true);
    expect(moved.feed.find('[data-test="events-actions"]').exists()).toBe(false);
  });

  it("gives the page the definitions the feed's own toolbar renders, keys and counts included", async () => {
    const service = fakeService(router());
    const own = mount(EventsFeed, {
      props: { client: service.client, catalog: 'ocharo', today: '2026-11-10', tracking: true, ...ocharo },
    });
    wrappers.push(own);
    await flushPromises();
    const rendered = own.getComponent(FacetBar).props('facets') as readonly FacetDefinition[];

    const { filters } = await mountPage(router());

    expect(filters.facetDefinitions.value).toEqual(rendered);
    expect(filters.facetDefinitions.value.map((item) => item.key)).toEqual([
      'scope',
      'kind',
      'stage',
      'hidden',
      'city',
      'source',
      'priority',
      'deadline',
      'when',
    ]);
    const scope = filters.facetDefinitions.value.find((item) => item.key === 'scope');
    expect(scope && scope.kind !== 'toggle' ? scope.options.map((option) => option.count) : []).toEqual([
      undefined,
      40,
      15,
      3,
    ]);
    expect(filters.matchCount.value).toBe(72);
    expect(filters.totalCount.value).toBe(72);
  });

  it('writes the same label props the feed gives its bar', async () => {
    const service = fakeService(router());
    for (const locale of ['en', 'ru', 'uz'] as const) {
      const wrapper = mount(EventsFeed, {
        props: { client: service.client, catalog: 'ocharo', today: '2026-11-10', tracking: true, locale },
      });
      wrappers.push(wrapper);
      await flushPromises();
      const bar = wrapper.getComponent(FacetBar);

      const labels = eventsFacetBarLabels(locale);

      expect(Object.keys(labels)).toEqual([
        'searchLabel',
        'searchPlaceholder',
        'filtersLabel',
        'clearAllLabel',
        'anyLabel',
        'selectedLabel',
        'removeLabel',
        'removeToggleLabel',
        'matchLabel',
      ]);
      for (const [key, value] of Object.entries(labels)) {
        expect(bar.props(key as keyof typeof labels)).toBe(value);
      }
    }
    expect(eventsFacetBarLabels('en').matchLabel).toBe('{matched} of {total}');
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
    const { filters, requests, wrapper } = await mountPage(router());
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

    wrapper.findComponent(FacetBar).vm.$emit('update:modelValue', { when: 'today' });
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
