import { flushPromises, mount } from '@vue/test-utils';
import { FacetBar, type FacetDefinition, type FacetSelection } from '@wow-two-beta/fe/presentation/forms';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { h } from 'vue';
import type {
  TrackedEventModel,
  TrackingFacetsModel,
  TrackingModel,
  TrackingSummaryModel,
  WorkflowModel,
} from '../src/contracts';
import { EventMediaCard, EventsFeed, type EventRegion } from '../src/kit';
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
const tracking = (eventId: string, stage: string, extra: Partial<TrackingModel> = {}): TrackingModel => ({
  id: `t-${eventId}`,
  eventId,
  stage,
  priority: 'normal',
  revision: 2,
  createdAtUtc: '2026-11-01T10:00:00Z',
  updatedAtUtc: '2026-11-02T10:00:00Z',
  ...extra,
});
const imported = eventModel('imported', {
  title: { 'ru-Cyrl': 'Выставка текстиля' },
  starts: { value: '2026-12-12', precision: 'day', certainty: 'announced' },
  place: { city: 'Tashkent', country: 'UZ' },
});
const shortlisted = eventModel('shortlisted', {
  title: { en: 'Fashion Week' },
  starts: { value: '2027-03-14', precision: 'day', certainty: 'announced' },
  place: { city: 'Almaty', country: 'KZ' },
});
const items: TrackedEventModel[] = [
  { event: imported, tracking: null },
  { event: shortlisted, tracking: tracking('shortlisted', 'shortlisted', { priority: 'high' }) },
];
const noDeadlines: TrackingSummaryModel = { byStage: {}, untracked: 0, upcomingDeadlines: [] };
const application = (eventId: string, day: string) => ({
  eventId,
  title: { en: `Event ${eventId}` },
  deadline: {
    kind: 'application' as const,
    at: { value: day, precision: 'day' as const, certainty: 'announced' as const },
  },
  stage: 'candidate',
});

interface Routes {
  /** Answers a list request (the tracking list's own URL); the one page of `items` by default. */
  list?: (url: URL) => { items: TrackedEventModel[]; nextCursor?: string | null };
  /** Answers a facets request; 72 events by default. */
  facets?: (url: URL) => TrackingFacetsModel;
  summary?: TrackingSummaryModel;
  put?: () => Response;
}

/** Answers each route of the tracking feed. */
function router(routes: Routes = {}) {
  return (request: Request) => {
    const url = new URL(request.url);
    const path = url.pathname;
    if (path.endsWith('/workflow')) return envelope(workflow);
    if (path.endsWith('/tracking/summary')) return envelope(routes.summary ?? noDeadlines);
    if (path.endsWith('/tracking/facets')) return envelope(routes.facets?.(url) ?? facetsModel(72));
    if (request.method === 'PUT') return routes.put?.() ?? envelope(tracking('imported', 'shortlisted'));
    const page = routes.list?.(url) ?? { items, nextCursor: null };
    return envelope({ nextCursor: null, ...page });
  };
}

const ocharo = {
  hiddenStages: ['skipped', 'rejected'],
  regions,
  quickStages: ['shortlisted', 'skipped'],
  canTrack: true,
};

async function render(props: Record<string, unknown>, responder: (request: Request) => Response, slots = {}) {
  const service = fakeService(responder);
  const wrapper = mount(EventsFeed, {
    props: { client: service.client, catalog: 'ocharo', today: '2026-11-10', tracking: true, ...props },
    slots,
  });
  wrappers.push(wrapper);
  await flushPromises();
  return { ...service, wrapper };
}

const listRequests = (requests: Request[]) =>
  requests.filter((request) => request.method === 'GET' && new URL(request.url).pathname.endsWith('/tracking'));
const facetRequests = (requests: Request[]) =>
  requests.filter((request) => new URL(request.url).pathname.endsWith('/tracking/facets'));
const lastParams = (requests: Request[]) => new URL(requests.at(-1)!.url).searchParams;

type Mounted = Awaited<ReturnType<typeof render>>['wrapper'];
const bar = (wrapper: Mounted) => wrapper.findComponent(FacetBar);
const facetOf = (wrapper: Mounted, key: string) =>
  (bar(wrapper).props('facets') as readonly FacetDefinition[]).find((item) => item.key === key);
const countsOf = (wrapper: Mounted, key: string) =>
  Object.fromEntries((facetOf(wrapper, key)?.options ?? []).map((option) => [option.value, option.count]));
/** Reports a choice the way the bar does, so the feed takes the whole selection. */
async function choose(wrapper: Mounted, selection: FacetSelection) {
  bar(wrapper).vm.$emit('update:modelValue', selection);
  await flushPromises();
}
async function typeSearch(wrapper: Mounted, value: string) {
  await wrapper.get('input[type="search"]').setValue(value);
  await new Promise((resolve) => setTimeout(resolve, 300));
  await flushPromises();
}
const pill = (wrapper: Mounted) => wrapper.get('[data-facet-match]').text();

describe('tracking feed (PG01, PG02, PG03, PG05, PG06, PG11)', () => {
  it("lists the organization's tracking as image-first cards, hiding skipped and rejected", async () => {
    const { wrapper, requests } = await render(ocharo, router());

    const query = new URL(listRequests(requests)[0]!.url).searchParams;
    expect(query.getAll('excludeStages')).toEqual(['skipped', 'rejected']);
    expect(query.get('from')).toBe('2026-11-10');
    expect(query.get('includeUndated')).toBe('true');
    expect(query.get('sort')).toBeNull();
    const cards = wrapper.findAll('[data-test="event-media-card"]');
    expect(cards).toHaveLength(2);
    expect(wrapper.findAll('[data-test="event-card"]')).toHaveLength(0);
    expect(wrapper.get('[data-test="event-cards"]').classes()).toContain(
      'grid-cols-[repeat(auto-fill,minmax(min(100%,17rem),1fr))]',
    );
    expect(cards[0]!.find('[data-test="event-stage"]').exists()).toBe(false);
    expect(cards[0]!.find('[data-test="event-scope"]').exists()).toBe(false);
    expect(cards[1]!.get('[data-test="event-stage"]').text()).toBe('shortlisted');
    expect(cards[1]!.get('[data-test="event-priority"]').attributes('aria-label')).toBe('High priority');
    expect(wrapper.find('[data-test="event-field-cost"]').exists()).toBe(false);
  });

  it('puts the toolbar on the facet bar: scope chips with counts, kind chips with counts, the rest in the popover', async () => {
    const { wrapper } = await render(
      {
        ...ocharo,
        kinds: [
          { key: 'runway', label: { en: 'Runway' } },
          { key: 'exhibition', label: { en: 'Expo' } },
        ],
      },
      router({
        facets: () =>
          facetsModel(72, {
            countries: [facetCount('UZ', 40), facetCount('KZ', 10), facetCount('RU', 5), facetCount('FR', 3)],
          }),
      }),
    );

    expect(bar(wrapper).props('facets')).toSatisfy(
      (list: readonly FacetDefinition[]) =>
        list.map((item) => `${item.key}:${item.placement}`).join(' ') ===
        'scope:inline kind:inline stage:popover hidden:popover city:popover source:popover priority:popover deadline:popover when:popover',
    );
    expect(countsOf(wrapper, 'scope')).toEqual({ all: undefined, local: 40, regional: 15, international: 3 });
    expect(countsOf(wrapper, 'kind')).toEqual({ runway: 2, exhibition: 0 });
    expect(facetOf(wrapper, 'kind')?.kind).toBe('multi');
    expect(countsOf(wrapper, 'city')).toEqual({ tashkent: 1, almaty: 1 });
    expect(facetOf(wrapper, 'city')?.options.map((option) => option.label)).toEqual(['Tashkent', 'Almaty']);
    expect(facetOf(wrapper, 'source')?.options.map((option) => option.label)).toEqual(['Afisha', 'Curated']);
    expect(countsOf(wrapper, 'priority')).toEqual({ high: 1 });
    expect(countsOf(wrapper, 'deadline')).toEqual({ soon: 1 });
    expect(facetOf(wrapper, 'when')?.options.map((option) => option.label)).toEqual([
      'Upcoming',
      'This month',
      'Next 3 months',
      'This year',
      'Past',
      'Custom range…',
    ]);
    expect(wrapper.findAll('button').some((button) => button.text() === 'Filters')).toBe(true);
  });

  it('shows "n of N": the current query\'s total over the unfiltered base total', async () => {
    const { wrapper, requests } = await render(
      ocharo,
      router({ facets: (url) => facetsModel(url.searchParams.has('text') ? 12 : 72) }),
    );
    expect(pill(wrapper)).toBe('72 of 72');
    const base = new URL(facetRequests(requests)[0]!.url).searchParams;
    expect(base.get('from')).toBe('2026-11-10');
    expect(base.getAll('excludeStages')).toEqual(['skipped', 'rejected']);

    await typeSearch(wrapper, 'fashion');

    expect(pill(wrapper)).toBe('12 of 72');
  });

  it('re-reads the facets with the search text, so the stage counts follow it', async () => {
    const { wrapper, requests } = await render(
      ocharo,
      router({
        facets: (url) =>
          url.searchParams.get('text') === 'fashion'
            ? facetsModel(1, {
                stages: [
                  facetCount('candidate', 0),
                  facetCount('shortlisted', 1),
                  facetCount('skipped', 0),
                  facetCount('rejected', 0),
                ],
              })
            : facetsModel(72),
      }),
    );
    expect(countsOf(wrapper, 'stage')).toEqual({ candidate: 2, shortlisted: 1, skipped: 2, rejected: 0 });

    await typeSearch(wrapper, 'fashion');

    expect(lastParams(facetRequests(requests)).get('text')).toBe('fashion');
    expect(lastParams(listRequests(requests)).get('text')).toBe('fashion');
    expect(countsOf(wrapper, 'stage')).toEqual({ candidate: 0, shortlisted: 1, skipped: 0, rejected: 0 });
  });

  it("reads one facets answer per query change, with the list's filters and no sort, cursor or limit", async () => {
    const { wrapper, requests } = await render(ocharo, router());
    const before = facetRequests(requests).length;

    await choose(wrapper, { city: ['tashkent'] });

    expect(facetRequests(requests)).toHaveLength(before + 1);
    const params = lastParams(facetRequests(requests));
    expect(params.getAll('cities')).toEqual(['tashkent']);
    expect(params.get('sort')).toBeNull();
    expect(params.get('limit')).toBeNull();
    expect(params.get('cursor')).toBeNull();
  });

  it('loads the next cursor when the sentinel scrolls into view, with no button', async () => {
    const view = stubIntersectionObserver();
    const more = eventModel('later', {
      title: { en: 'Autumn Fair' },
      starts: { value: '2027-04-01', precision: 'day', certainty: 'announced' },
    });
    const { wrapper, requests } = await render(
      ocharo,
      router({
        list: (url) =>
          url.searchParams.get('cursor') === 'c2'
            ? { items: [{ event: more, tracking: null }], nextCursor: null }
            : { items, nextCursor: 'c2' },
      }),
    );

    expect(listRequests(requests)).toHaveLength(1);
    expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(2);
    expect(wrapper.findAll('button').some((button) => /load more/i.test(button.text()))).toBe(false);
    expect(view.watching()).toBe(1);

    view.fire();
    await flushPromises();

    expect(listRequests(requests)).toHaveLength(2);
    expect(lastParams(listRequests(requests)).get('cursor')).toBe('c2');
    expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(3);
    expect(view.watching()).toBe(0);
  });

  it('sends the sort with the list, starts the cursor over and drops the month dividers outside `starts`', async () => {
    const view = stubIntersectionObserver();
    const { wrapper, requests } = await render(
      ocharo,
      router({ list: (url) => ({ items, nextCursor: url.searchParams.get('cursor') ? null : 'c2' }) }),
    );
    expect(wrapper.findAll('[data-test="event-group"] > h3').map((heading) => heading.text())).toEqual([
      'December 2026',
      'March 2027',
    ]);
    view.fire();
    await flushPromises();
    expect(lastParams(listRequests(requests)).get('cursor')).toBe('c2');
    const before = listRequests(requests).length;

    await wrapper.get('select[name="sort"]').setValue('added');
    await flushPromises();

    expect(wrapper.findAll('select[name="sort"] option').map((option) => option.text())).toEqual([
      'Start date',
      'Recently added',
      'Nearest deadline',
    ]);
    expect(listRequests(requests)).toHaveLength(before + 1);
    expect(lastParams(listRequests(requests)).get('sort')).toBe('added');
    expect(lastParams(listRequests(requests)).get('cursor')).toBeNull();
    expect(wrapper.findAll('[data-test="event-group"] > h3')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="event-group"]')).toHaveLength(1);
    expect(wrapper.findAll('[data-test="event-media-card"]').map((card) => card.get('h3').text())).toEqual([
      'Выставка текстиля',
      'Fashion Week',
    ]);

    await wrapper.get('select[name="sort"]').setValue('starts');
    await flushPromises();
    expect(wrapper.findAll('[data-test="event-group"] > h3')).toHaveLength(2);
  });

  it('turns a popover choice into a removable chip, and removing it drops the filter', async () => {
    const { wrapper, requests } = await render(ocharo, router());

    await choose(wrapper, { city: ['tashkent'], priority: 'high' });

    expect(lastParams(listRequests(requests)).getAll('cities')).toEqual(['tashkent']);
    expect(lastParams(listRequests(requests)).get('priority')).toBe('high');
    const chips = wrapper.get('[data-facet-chips]');
    expect(chips.text()).toContain('City: Tashkent');
    expect(chips.text()).toContain('Priority: High');
    expect(wrapper.get('[data-facet-filters-count]').text()).toBe('2');

    await wrapper.get('[aria-label="Remove City: Tashkent"]').trigger('click');
    await flushPromises();

    expect(wrapper.get('[data-facet-chips]').text()).not.toContain('City: Tashkent');
    expect(lastParams(listRequests(requests)).getAll('cities')).toEqual([]);
    expect(lastParams(listRequests(requests)).get('priority')).toBe('high');
  });

  it('shows hidden stages on request or by their own filter, and filters by scope chips', async () => {
    const { wrapper, requests } = await render(ocharo, router());

    await choose(wrapper, { hidden: 'show' });
    expect(lastParams(listRequests(requests)).getAll('excludeStages')).toEqual([]);

    await choose(wrapper, { hidden: 'show', stage: ['skipped'] });
    expect(lastParams(listRequests(requests)).getAll('stages')).toEqual(['skipped']);
    expect(facetOf(wrapper, 'hidden')).toBeUndefined();

    await choose(wrapper, {});
    const regional = wrapper.findAll('button').find((button) => button.text().startsWith('Regional'))!;
    await regional.trigger('click');
    await flushPromises();
    expect(lastParams(listRequests(requests)).getAll('countries')).toEqual(['KZ', 'RU']);

    await choose(wrapper, { scope: 'international' });
    const international = lastParams(listRequests(requests));
    expect(international.getAll('excludeCountries')).toEqual(['UZ', 'KZ', 'RU']);
    expect(international.get('includeUnknownCountry')).toBeNull();

    await choose(wrapper, { scope: 'all' });
    expect(lastParams(listRequests(requests)).getAll('countries')).toEqual([]);
    expect(lastParams(listRequests(requests)).getAll('excludeCountries')).toEqual([]);
  });

  it('turns the date presets into from and to, the default listing undated events', async () => {
    const { wrapper, requests } = await render(ocharo, router());

    for (const [preset, from, to] of [
      ['thisMonth', '2026-11-01', '2026-11-30'],
      ['next3Months', '2026-11-10', '2027-02-10'],
      ['thisYear', '2026-01-01', '2026-12-31'],
      ['past', null, '2026-11-09'],
    ] as const) {
      await choose(wrapper, { when: preset });
      const params = lastParams(listRequests(requests));
      expect(params.get('from'), preset).toBe(from);
      expect(params.get('to'), preset).toBe(to);
      expect(params.get('includeUndated'), preset).toBeNull();
    }

    await choose(wrapper, { when: 'upcoming' });
    expect(lastParams(listRequests(requests)).get('from')).toBe('2026-11-10');
    expect(lastParams(listRequests(requests)).get('includeUndated')).toBe('true');
    expect(wrapper.find('[data-facet-chips]').exists()).toBe(false);
  });

  it('moves an untracked event from the stage menu on its card, then reloads the list and the counts', async () => {
    const { wrapper, requests } = await render(ocharo, router());
    const facetsBefore = facetRequests(requests).length;

    const cards = wrapper.findAll('[data-test="event-media-card"]');
    expect(cards[1]!.find('[data-test="event-stage-menu"]').exists()).toBe(false);
    await cards[0]!.get('[data-test="event-stage-menu"]').trigger('click');
    await flushPromises();
    const entries = [...document.querySelectorAll<HTMLElement>('[data-test="event-quick-stage"]')];
    expect(entries.map((entry) => entry.textContent?.trim())).toEqual(['Shortlisted', 'Skipped']);
    entries[0]!.click();
    await flushPromises();

    const put = requests.find((request) => request.method === 'PUT')!;
    expect(new URL(put.url).pathname).toBe('/api/events/v1/catalogs/ocharo/tracking/imported');
    expect(await put.json()).toEqual({ stage: 'shortlisted', expectedRevision: null });
    expect(listRequests(requests)).toHaveLength(2);
    expect(facetRequests(requests).length).toBeGreaterThan(facetsBefore);
    expect(wrapper.emitted('tracked')).toHaveLength(1);
    expect(wrapper.emitted('open')).toBeUndefined();
  });

  it('reloads and says so when the quick action meets a newer version', async () => {
    const conflict = () =>
      Response.json({ title: 'Conflict' }, { status: 409, headers: { 'Content-Type': 'application/problem+json' } });
    const { wrapper, requests } = await render(ocharo, router({ put: conflict }));

    await wrapper.findAll('[data-test="event-media-card"]')[0]!.get('[data-test="event-stage-menu"]').trigger('click');
    await flushPromises();
    document.querySelectorAll<HTMLElement>('[data-test="event-quick-stage"]')[1]!.click();
    await flushPromises();

    expect(wrapper.get('[data-test="track-changed"]').text()).toContain('changed meanwhile');
    expect(listRequests(requests)).toHaveLength(2);
    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
  });

  it('keeps the cards a plain grid of EventMediaCard', async () => {
    const { wrapper } = await render({ ...ocharo, eventHref: (id: string) => `/events/${id}` }, router());

    expect(wrapper.findAllComponents(EventMediaCard)).toHaveLength(2);
    expect(wrapper.findAll('h3 a').map((link) => link.attributes('href'))).toEqual([
      '/events/imported',
      '/events/shortlisted',
    ]);
  });
});

describe('card slots in tracking mode', () => {
  it('card-actions gets the quick stages and a track function that writes the stage', async () => {
    const { wrapper, requests } = await render(ocharo, router(), {
      'card-actions': (scope: {
        event: { id: string };
        quickStages: readonly string[];
        track: (stage: string) => void;
      }) =>
        h(
          'button',
          {
            type: 'button',
            'data-test': 'custom-action',
            onClick: () => scope.track('shortlisted'),
          },
          `${scope.event.id}:${scope.quickStages.join(',')}`,
        ),
    });

    const buttons = wrapper.findAll('[data-test="custom-action"]');
    expect(buttons.map((button) => button.text())).toEqual(['imported:shortlisted,skipped', 'shortlisted:']);
    expect(wrapper.findAll('[data-test="event-stage-menu"]')).toHaveLength(0);

    await buttons[0]!.trigger('click');
    await flushPromises();

    const put = requests.find((request) => request.method === 'PUT')!;
    expect(new URL(put.url).pathname).toBe('/api/events/v1/catalogs/ocharo/tracking/imported');
    expect(await put.json()).toEqual({ stage: 'shortlisted', expectedRevision: null });
    expect(wrapper.emitted('tracked')).toHaveLength(1);
  });

  it('card-pills sees the stage label of a tracked event', async () => {
    const { wrapper } = await render(ocharo, router(), {
      'card-pills': (scope: { stage: string }) => h('span', { 'data-test': 'custom-pills' }, scope.stage || '-'),
    });

    expect(wrapper.findAll('[data-test="custom-pills"]').map((item) => item.text())).toEqual(['-', 'Shortlisted']);
  });
});

describe('attention pills', () => {
  it('hides the deadlines pill at zero and shows it above zero, expanding to the deadlines panel', async () => {
    const none = await render(ocharo, router());
    expect(none.wrapper.find('[data-test="attention-pills"]').exists()).toBe(false);
    expect(none.wrapper.find('[data-pill="deadlines"]').exists()).toBe(false);

    const summary: TrackingSummaryModel = {
      byStage: {},
      untracked: 0,
      upcomingDeadlines: [
        application('imported', '2026-11-20'),
        application('shortlisted', '2026-11-24'),
        application('far', '2026-12-30'),
        {
          ...application('registration', '2026-11-12'),
          deadline: { kind: 'registration', at: { value: '2026-11-12', precision: 'day', certainty: 'announced' } },
        },
      ],
    };
    const { wrapper, requests } = await render(ocharo, router({ summary }));

    const button = wrapper.get('[data-pill="deadlines"]');
    expect(button.text()).toBe('2 deadlines in 14 days');
    expect(wrapper.find('[data-test="attention-panel"]').exists()).toBe(false);
    const summaryRequest = requests.find((request) => new URL(request.url).pathname.endsWith('/tracking/summary'))!;
    expect(new URL(summaryRequest.url).searchParams.get('today')).toBe('2026-11-10');
    expect(new URL(summaryRequest.url).searchParams.getAll('deadlineKinds')).toEqual(['application']);

    await button.trigger('click');
    await flushPromises();

    expect(wrapper.get('[data-test="attention-panel"] h2').text()).toBe('Next deadlines');
    expect(wrapper.findAll('[data-test="deadline"]').length).toBeGreaterThan(0);
    await button.trigger('click');
    expect(wrapper.find('[data-test="attention-panel"]').exists()).toBe(false);
  });

  it('says "1 deadline" for one and hides the pill when none falls in the 14 days', async () => {
    const one = await render(
      ocharo,
      router({ summary: { ...noDeadlines, upcomingDeadlines: [application('imported', '2026-11-24')] } }),
    );
    expect(one.wrapper.get('[data-pill="deadlines"]').text()).toBe('1 deadline in 14 days');

    const late = await render(
      ocharo,
      router({ summary: { ...noDeadlines, upcomingDeadlines: [application('imported', '2026-11-25')] } }),
    );
    expect(late.wrapper.find('[data-pill="deadlines"]').exists()).toBe(false);
  });

  it("shows a consumer's pill above zero only, and renders its slot with `key` and `close` on expand", async () => {
    const { wrapper } = await render(
      {
        ...ocharo,
        attention: [
          { key: 'past', label: '2 past to close', count: 2 },
          { key: 'quiet', label: 'Nothing here', count: 0 },
        ],
      },
      router(),
      {
        attention: (slot: { key: string; close: () => void }) =>
          h('div', { 'data-test': 'past-strip' }, [
            `strip for ${slot.key}`,
            h('button', { 'data-test': 'strip-close', onClick: slot.close }, 'close'),
          ]),
      },
    );

    expect(wrapper.findAll('[data-test="attention-pills"] button').map((button) => button.text())).toEqual([
      '2 past to close',
    ]);
    expect(wrapper.find('[data-test="past-strip"]').exists()).toBe(false);

    await wrapper.get('[data-pill="past"]').trigger('click');
    expect(wrapper.get('[data-test="past-strip"]').text()).toContain('strip for past');
    expect(wrapper.get('[data-pill="past"]').attributes('aria-expanded')).toBe('true');

    await wrapper.get('[data-test="strip-close"]').trigger('click');
    expect(wrapper.find('[data-test="past-strip"]').exists()).toBe(false);
  });
});
