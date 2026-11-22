import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { h } from 'vue';
import type {
  TrackedEventModel,
  TrackingFacetsModel,
  TrackingModel,
  TrackingSummaryModel,
  WorkflowModel,
} from '../src/contracts';
import {
  EventMediaCard,
  eventsFilterLabels,
  type EventRegion,
  type EventsFacet,
  type EventsFilters,
  type EventsFilterSelection,
} from '../src/kit';
import { envelope, eventModel, facetCount, facetsModel, stubIntersectionObserver } from './Fixtures';
import { mountFeed } from './FeedPage';
import { openPicker, panelLabels, pick, pickOption, submitted } from './Picker';
import { desktop, heightAt, phone, utilitiesAt } from './Viewport';

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

/** Mounts the feed the way a page does, with the page's own filter state in tracking mode (`mountFeed`). */
async function render(props: Record<string, unknown>, responder: (request: Request) => Response, slots = {}) {
  const mounted = await mountFeed({ tracking: true, ...props }, responder, { slots });
  wrappers.push(mounted.host);
  return mounted;
}

const listRequests = (requests: Request[]) =>
  requests.filter((request) => request.method === 'GET' && new URL(request.url).pathname.endsWith('/tracking'));
const facetRequests = (requests: Request[]) =>
  requests.filter((request) => new URL(request.url).pathname.endsWith('/tracking/facets'));
const lastParams = (requests: Request[]) => new URL(requests.at(-1)!.url).searchParams;

type Mounted = Awaited<ReturnType<typeof render>>['wrapper'];
const facetOf = (filters: EventsFilters, key: string): EventsFacet | undefined =>
  filters.facetDefinitions.value.find((item) => item.key === key);
/** The options of a choice facet; a toggle has none. */
const optionsOf = (filters: EventsFilters, key: string) => {
  const facet = facetOf(filters, key);
  return facet && facet.kind !== 'toggle' ? facet.options : [];
};
const countsOf = (filters: EventsFilters, key: string) =>
  Object.fromEntries(optionsOf(filters, key).map((option) => [option.value, option.count]));
/** Writes a choice the way a page's bar does, so the state takes the whole selection. */
async function choose(filters: EventsFilters, selection: EventsFilterSelection) {
  filters.selection.value = selection;
  await flushPromises();
}
async function typeSearch(filters: EventsFilters, value: string) {
  filters.search.value = value;
  await new Promise((resolve) => setTimeout(resolve, 300));
  await flushPromises();
}
/** The match pill a page's bar shows: the current query's total over the base total. */
const pill = (filters: EventsFilters) => `${filters.matchCount.value} of ${filters.totalCount.value}`;

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

  it('gives the page the facets of the toolbar: scope and kind inline with counts, the rest behind the Filters button', async () => {
    const { filters } = await render(
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

    expect(filters.facetDefinitions.value.map((item) => `${item.key}:${item.placement}`).join(' ')).toBe(
      'scope:inline kind:inline stage:popover hidden:popover city:popover source:popover priority:popover deadline:popover when:popover',
    );
    expect(countsOf(filters, 'scope')).toEqual({ all: undefined, local: 40, regional: 15, international: 3 });
    expect(countsOf(filters, 'kind')).toEqual({ runway: 2, exhibition: 0 });
    expect(facetOf(filters, 'kind')?.kind).toBe('multi');
    expect(countsOf(filters, 'city')).toEqual({ tashkent: 1, almaty: 1 });
    expect(optionsOf(filters, 'city').map((option) => option.label)).toEqual(['Tashkent', 'Almaty']);
    expect(optionsOf(filters, 'source').map((option) => option.label)).toEqual(['Afisha', 'Curated']);
    expect(facetOf(filters, 'priority')).toEqual({
      key: 'priority',
      label: 'High priority',
      kind: 'toggle',
      placement: 'popover',
      count: 1,
    });
    expect(facetOf(filters, 'deadline')).toEqual({
      key: 'deadline',
      label: 'Deadline within 30 days',
      kind: 'toggle',
      placement: 'popover',
      count: 1,
    });
    expect(facetOf(filters, 'hidden')).toEqual({
      key: 'hidden',
      label: 'Show Skipped, Rejected',
      kind: 'toggle',
      placement: 'popover',
    });
    expect(optionsOf(filters, 'when').map((option) => option.label)).toEqual([
      'Upcoming',
      'Today',
      'This month',
      'Next 3 months',
      'This year',
      'Past',
      'Custom range…',
    ]);
  });

  it('counts "n of N": the current query\'s total over the unfiltered base total', async () => {
    const { filters, requests } = await render(
      ocharo,
      router({ facets: (url) => facetsModel(url.searchParams.has('text') ? 12 : 72) }),
    );
    expect(pill(filters)).toBe('72 of 72');
    const base = new URL(facetRequests(requests)[0]!.url).searchParams;
    expect(base.get('from')).toBe('2026-11-10');
    expect(base.getAll('excludeStages')).toEqual(['skipped', 'rejected']);

    await typeSearch(filters, 'fashion');

    expect(pill(filters)).toBe('12 of 72');
  });

  it('re-reads the facets with the search text, so the stage counts follow it', async () => {
    const { filters, requests } = await render(
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
    expect(countsOf(filters, 'stage')).toEqual({ candidate: 2, shortlisted: 1, skipped: 2, rejected: 0 });

    await typeSearch(filters, 'fashion');

    expect(lastParams(facetRequests(requests)).get('text')).toBe('fashion');
    expect(lastParams(listRequests(requests)).get('text')).toBe('fashion');
    expect(countsOf(filters, 'stage')).toEqual({ candidate: 0, shortlisted: 1, skipped: 0, rejected: 0 });
  });

  it("reads one facets answer per query change, with the list's filters and no sort, cursor or limit", async () => {
    const { filters, requests } = await render(ocharo, router());
    const before = facetRequests(requests).length;

    await choose(filters, { city: ['tashkent'] });

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

  it('opens the sort as the sdk panel, as a pill, with no native select, and keeps the picked key', async () => {
    const { wrapper, filters } = await render(ocharo, router());
    const trigger = wrapper.get('[data-test="feed-sort"]');

    expect(wrapper.element.querySelectorAll('select')).toHaveLength(0);
    expect(trigger.attributes('aria-label')).toBe('Sort');
    expect(trigger.classes()).toEqual(expect.arrayContaining(['h-10', 'rounded-full']));
    expect(trigger.classes()).not.toContain('rounded-md');
    expect(trigger.text()).toBe('Start date');
    expect(submitted(wrapper.element, 'sort')).toBe('starts');

    await openPicker(trigger.element);
    const layer = document.querySelector('[role="dialog"]');
    expect(layer?.querySelector('[role="listbox"]')).not.toBeNull();
    expect(wrapper.element.contains(layer)).toBe(false);
    expect(document.querySelectorAll('select')).toHaveLength(0);
    expect(layer?.querySelector('[role="option"][aria-selected="true"]')?.textContent?.trim()).toBe('Start date');
    await pickOption('Nearest deadline');

    expect(trigger.text()).toBe('Nearest deadline');
    expect(submitted(wrapper.element, 'sort')).toBe('deadline');
    expect(filters.sort.value).toBe('deadline');
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

    await openPicker(wrapper.get('[data-test="feed-sort"]').element);
    expect(panelLabels()).toEqual(['Start date', 'Recently added', 'Nearest deadline']);
    await pickOption('Recently added');
    await flushPromises();

    expect(listRequests(requests)).toHaveLength(before + 1);
    expect(lastParams(listRequests(requests)).get('sort')).toBe('added');
    expect(lastParams(listRequests(requests)).get('cursor')).toBeNull();
    expect(wrapper.findAll('[data-test="event-group"] > h3')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="event-group"]')).toHaveLength(1);
    expect(wrapper.findAll('[data-test="event-media-card"]').map((card) => card.get('h3').text())).toEqual([
      'Выставка текстиля',
      'Fashion Week',
    ]);

    await pick(wrapper.get('[data-test="feed-sort"]').element, 'Start date');
    await flushPromises();
    expect(wrapper.findAll('[data-test="event-group"] > h3')).toHaveLength(2);
  });

  it('lists with every pick in the selection, and writing the selection without one drops that filter', async () => {
    const { filters, requests } = await render(ocharo, router());

    await choose(filters, { city: ['tashkent'], priority: true });

    expect(lastParams(listRequests(requests)).getAll('cities')).toEqual(['tashkent']);
    expect(lastParams(listRequests(requests)).get('priority')).toBe('high');
    expect(filters.filtered.value).toBe(true);

    await choose(filters, { priority: true });

    expect(lastParams(listRequests(requests)).getAll('cities')).toEqual([]);
    expect(lastParams(listRequests(requests)).get('priority')).toBe('high');
  });

  it('shows hidden stages on request or by their own filter, and filters by scope', async () => {
    const { filters, requests } = await render(ocharo, router());

    await choose(filters, { hidden: true });
    expect(lastParams(listRequests(requests)).getAll('excludeStages')).toEqual([]);

    await choose(filters, { hidden: true, stage: ['skipped'] });
    expect(lastParams(listRequests(requests)).getAll('stages')).toEqual(['skipped']);
    expect(facetOf(filters, 'hidden')).toBeUndefined();

    await choose(filters, { scope: 'regional' });
    expect(lastParams(listRequests(requests)).getAll('countries')).toEqual(['KZ', 'RU']);

    await choose(filters, { scope: 'international' });
    const international = lastParams(listRequests(requests));
    expect(international.getAll('excludeCountries')).toEqual(['UZ', 'KZ', 'RU']);
    expect(international.get('includeUnknownCountry')).toBeNull();

    await choose(filters, { scope: 'all' });
    expect(lastParams(listRequests(requests)).getAll('countries')).toEqual([]);
    expect(lastParams(listRequests(requests)).getAll('excludeCountries')).toEqual([]);
  });

  it('turns the date presets into from and to, the default listing undated events', async () => {
    const { filters, requests } = await render(ocharo, router());

    for (const [preset, from, to] of [
      ['thisMonth', '2026-11-01', '2026-11-30'],
      ['next3Months', '2026-11-10', '2027-02-10'],
      ['thisYear', '2026-01-01', '2026-12-31'],
      ['past', null, '2026-11-09'],
    ] as const) {
      await choose(filters, { when: preset });
      const params = lastParams(listRequests(requests));
      expect(params.get('from'), preset).toBe(from);
      expect(params.get('to'), preset).toBe(to);
      expect(params.get('includeUndated'), preset).toBeNull();
    }

    await choose(filters, { when: 'upcoming' });
    expect(lastParams(listRequests(requests)).get('from')).toBe('2026-11-10');
    expect(lastParams(listRequests(requests)).get('includeUndated')).toBe('true');
    // The default preset is no choice: the selection holds nothing.
    expect(filters.selection.value).toEqual({});
    expect(filters.filtered.value).toBe(false);
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

describe('toggle facets', () => {
  it('sets the priority query with "High priority", clears it, and dropping it from the selection removes it', async () => {
    const { filters, requests } = await render(ocharo, router());
    expect(lastParams(listRequests(requests)).get('priority')).toBeNull();

    await choose(filters, { priority: true });

    expect(lastParams(listRequests(requests)).get('priority')).toBe('high');
    expect(lastParams(facetRequests(requests)).get('priority')).toBe('high');
    expect(facetOf(filters, 'priority')?.kind).toBe('toggle');

    await choose(filters, {});
    expect(lastParams(listRequests(requests)).get('priority')).toBeNull();
    expect(lastParams(facetRequests(requests)).get('priority')).toBeNull();
    expect(filters.filtered.value).toBe(false);

    await choose(filters, { priority: true });
    await choose(filters, {});
    expect(lastParams(listRequests(requests)).get('priority')).toBeNull();
  });

  it('sets the deadline query 30 days from today with "Deadline within 30 days", and clears it', async () => {
    const { filters, requests } = await render(ocharo, router());
    expect(lastParams(listRequests(requests)).get('deadlineBefore')).toBeNull();

    await choose(filters, { deadline: true });

    expect(lastParams(listRequests(requests)).get('deadlineBefore')).toBe('2026-12-10');
    expect(lastParams(facetRequests(requests)).get('deadlineBefore')).toBe('2026-12-10');

    await choose(filters, {});
    expect(lastParams(listRequests(requests)).get('deadlineBefore')).toBeNull();

    await choose(filters, { deadline: true });
    await choose(filters, {});
    expect(lastParams(listRequests(requests)).get('deadlineBefore')).toBeNull();
  });

  it('lifts the hidden-stage exclusion with "Show {hidden stages}" and restores it on clear', async () => {
    const { filters, requests } = await render(ocharo, router());
    expect(lastParams(listRequests(requests)).getAll('excludeStages')).toEqual(['skipped', 'rejected']);

    await choose(filters, { hidden: true });

    expect(lastParams(listRequests(requests)).getAll('excludeStages')).toEqual([]);
    expect(lastParams(facetRequests(requests)).getAll('excludeStages')).toEqual([]);
    expect(facetOf(filters, 'hidden')?.label).toBe('Show Skipped, Rejected');

    await choose(filters, {});
    expect(lastParams(listRequests(requests)).getAll('excludeStages')).toEqual(['skipped', 'rejected']);

    await choose(filters, { hidden: true });
    await choose(filters, {});
    expect(lastParams(listRequests(requests)).getAll('excludeStages')).toEqual(['skipped', 'rejected']);
  });

  it('carries the toggles with their server counts, in the order of the Filters button', async () => {
    const { filters } = await render(ocharo, router());

    const toggles = filters.facetDefinitions.value.filter((facet) => facet.kind === 'toggle');

    expect(toggles.map((facet) => facet.label)).toEqual([
      'Show Skipped, Rejected',
      'High priority',
      'Deadline within 30 days',
    ]);
    expect(toggles.map((facet) => (facet.kind === 'toggle' ? facet.count : null))).toEqual([undefined, 1, 1]);
    expect(toggles.every((facet) => facet.placement === 'popover')).toBe(true);
  });

  it('names a toggle facet in each language, and the close button its label asks for', async () => {
    for (const [locale, label, name] of [
      ['ru', 'Высокий приоритет', 'Убрать: Высокий приоритет'],
      ['uz', 'Yuqori ustuvorlik', 'Olib tashlash: Yuqori ustuvorlik'],
    ] as const) {
      const { filters } = await render({ ...ocharo, locale }, router());

      expect(facetOf(filters, 'priority')?.label, locale).toBe(label);
      expect(eventsFilterLabels(locale).removeToggleLabel.replace('{facet}', label), locale).toBe(name);
    }
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

/**
 * The feed's actions row and its proposal form at the two widths of the dev review (390 px and 1440 px): rows 12, 13, 15
 * and 16 of the Events app review. happy-dom does no layout, so `Viewport` reads the classes a browser would apply. The
 * filter bar is the page's, so only the controls the feed renders are read.
 */
describe('feed actions on a phone and a desktop (events review #12, #13, #15, #16)', () => {
  const phoneFeed = {
    ...ocharo,
    canPropose: true,
    attention: [{ key: 'past', label: '1 past to close', count: 1 }],
  };

  /** Mounts the feed in the document, so focus and scrolling can be read. */
  async function attached(props: Record<string, unknown> = phoneFeed) {
    const mounted = await mountFeed({ tracking: true, ...props }, router(), { attachTo: document.body });
    wrappers.push(mounted.host);
    return mounted.wrapper;
  }
  const propose = (wrapper: Mounted) =>
    wrapper.findAll('button').find((button) => button.text() === 'Propose an event')!;
  const proposalForm = (wrapper: Mounted) => wrapper.find('section[aria-label="Propose an event"]');
  const overflow = (wrapper: Mounted) => wrapper.get('[data-test="feed-overflow"]');
  const sort = (wrapper: Mounted) => wrapper.get('[data-test="feed-sort"]');
  const attentionPill = (wrapper: Mounted) => wrapper.get('[data-pill="past"]');

  it('#12: every control of the actions row and the attention pill is at least 40 px high on a phone', async () => {
    const wrapper = await attached();

    for (const control of [sort(wrapper), propose(wrapper), overflow(wrapper), attentionPill(wrapper)]) {
      expect(heightAt(control.element, phone)).toBeGreaterThanOrEqual(40);
    }
    // The pill keeps its smaller desktop height; the actions row's controls are 40 px at both widths (#16).
    expect(heightAt(attentionPill(wrapper).element, desktop)).toBe(32);
  });

  it('#12: the card menu button and the menu items reach 40 px on a phone, and stay as they were on a desktop', async () => {
    const wrapper = await attached();

    const trigger = wrapper.get('[data-test="event-stage-menu"]');
    expect(heightAt(trigger.element, phone)).toBe(40);
    expect(heightAt(trigger.element, desktop)).toBe(32);
    await trigger.trigger('click');
    await flushPromises();
    const items = [...document.querySelectorAll('[data-test="event-quick-stage"]')];
    expect(items).toHaveLength(2);
    for (const item of items) {
      expect(heightAt(item, phone)).toBeGreaterThanOrEqual(40);
      expect(heightAt(item, desktop)).toBe(0);
    }
  });

  it('#12: the overflow menu’s calendar item reaches 40 px on a phone', async () => {
    const wrapper = await attached();

    await overflow(wrapper).trigger('click');
    await flushPromises();
    const item = document.querySelector('[data-test="download-calendar"]')!;
    expect(heightAt(item, phone)).toBeGreaterThanOrEqual(40);
  });

  it('#16: the sort, Propose an event and the overflow button are one height at 1440 px', async () => {
    const wrapper = await attached();

    const heights = [sort(wrapper), propose(wrapper), overflow(wrapper)].map((control) =>
      heightAt(control.element, desktop),
    );
    expect(heights).toEqual([40, 40, 40]);
  });

  it('#15: on a phone the sort and Propose an event grow to close their row, and the overflow button does not', async () => {
    const wrapper = await attached();

    // The sort's trigger sits in its own box, which is what grows.
    for (const control of [sort(wrapper).element.closest('div')!, propose(wrapper).element]) {
      expect(utilitiesAt(control, phone)).toContain('grow');
      expect(utilitiesAt(control, desktop)).not.toContain('grow');
    }
    expect(utilitiesAt(overflow(wrapper).element, phone)).not.toContain('grow');
  });

  it('#13: opening the proposal form scrolls it into view and moves focus to its first field', async () => {
    const scrolled = vi.fn();
    const original = Element.prototype.scrollIntoView;
    Element.prototype.scrollIntoView = function (this: Element, ...args: unknown[]) {
      scrolled(this, ...args);
    } as typeof Element.prototype.scrollIntoView;
    try {
      const wrapper = await attached();
      expect(proposalForm(wrapper).exists()).toBe(false);

      await propose(wrapper).trigger('click');
      await flushPromises();

      const form = proposalForm(wrapper).element;
      expect(scrolled).toHaveBeenCalledTimes(1);
      expect(scrolled.mock.calls[0]![0]).toBe(form);
      expect(scrolled.mock.calls[0]![1]).toMatchObject({ block: 'start' });
      expect(document.activeElement).toBe(form.querySelector('input[name="title"]'));
      // The section clears a sticky header when it scrolls to the top.
      expect(form.classList.contains('scroll-mt-20')).toBe(true);
    } finally {
      Element.prototype.scrollIntoView = original;
    }
  });
});
