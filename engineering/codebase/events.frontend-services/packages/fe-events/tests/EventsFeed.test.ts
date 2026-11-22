import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { h } from 'vue';
import type { EventImage, EventModel } from '../src/contracts';
import { EventMediaCard, type EventCardDetailSlot } from '../src/kit';
import { envelope, eventModel, facetsModel, fakeService, stubIntersectionObserver } from './Fixtures';
import { mountFeed } from './FeedPage';
import { pick } from './Picker';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

const events = [
  eventModel('undated', { title: { und: 'Someday Market' } }),
  eventModel('october', {
    title: { ru: 'Неделя моды', und: 'Fashion Week' },
    starts: { value: '2026-10', precision: 'month', certainty: 'estimated' },
    images: [{ url: 'https://cdn.example.test/curated.jpg', attribution: 'Ocharo' }],
  }),
  eventModel('march', {
    title: { und: 'Spring Fair' },
    starts: { value: '2027-03-14', precision: 'day', certainty: 'announced' },
    ends: { value: '2027-03-16', precision: 'day', certainty: 'announced' },
    deadlines: [{ kind: 'application', at: { value: '2026-11-20', precision: 'day', certainty: 'announced' } }],
  }),
];
const page = () => envelope({ items: events, nextCursor: null });

/** Answers the routes of a tracking feed: an empty list, 72 events in the facets, no deadlines. */
function trackingRoutes(request: Request) {
  const path = new URL(request.url).pathname;
  if (path.endsWith('/tracking/facets')) return envelope(facetsModel(72));
  if (path.endsWith('/workflow')) return envelope({ stages: [], defaultStage: 'candidate', revision: 1 });
  if (path.endsWith('/tracking/summary')) return envelope({ byStage: {}, untracked: 0, upcomingDeadlines: [] });
  return envelope({ items: [], nextCursor: null });
}

/** Mounts the feed the way a page does, with the page's own filter state (`mountFeed`). */
async function render(props: Record<string, unknown>, ...responders: Parameters<typeof fakeService>) {
  const mounted = await mountFeed(props, responders.length > 1 ? sequence(responders) : responders[0]!);
  wrappers.push(mounted.host);
  return mounted;
}

/** Answers each request with the next responder, the last one for every request after. */
function sequence(responders: Parameters<typeof fakeService>) {
  const rest = [...responders];
  return (request: Request) => (rest.length > 1 ? rest.shift()! : rest[0]!)(request);
}

describe('events feed', () => {
  it('asks for upcoming published events and groups them by month, undated last', async () => {
    const { wrapper, requests } = await render({}, page);

    const query = Object.fromEntries(new URL(requests[0]!.url).searchParams);
    expect(query).toMatchObject({ review: 'published', includeUndated: 'true', from: '2026-11-10', limit: '50' });
    expect(wrapper.findAll('[data-test="event-group"] > h3').map((heading) => heading.text())).toEqual([
      'October 2026',
      'March 2027',
      'Date not announced',
    ]);
    const dates = wrapper.findAll('[data-test="event-dates"]').map((item) => item.text());
    expect(dates).toEqual(['≈ Oct 2026 (expected)', '14–16 Mar 2027', 'Dates not announced']);
    expect(wrapper.get('[data-test="event-deadline"]').text()).toContain('apply by 20 Nov');
  });

  it('shows curated images only and writes in the chosen language', async () => {
    const { wrapper } = await render({ locale: 'ru' }, page);

    expect(wrapper.findAll('img').map((image) => image.attributes('src'))).toEqual([
      'https://cdn.example.test/curated.jpg',
    ]);
    expect(wrapper.text()).toContain('Неделя моды');
    expect(wrapper.text()).toContain('≈ окт. 2026 (ожидается)');
  });

  it('lists events as image-first cards in a grid, keeping the month headers', async () => {
    const { wrapper } = await render({ eventHref: (id: string) => `/events/${id}` }, page);

    expect(wrapper.findAll('[data-test="event-group"] > h3').map((heading) => heading.text())).toEqual([
      'October 2026',
      'March 2027',
      'Date not announced',
    ]);
    expect(wrapper.findAllComponents(EventMediaCard)).toHaveLength(3);
    const grids = wrapper.findAll('[data-test="event-cards"]');
    expect(grids).toHaveLength(3);
    expect(grids[0]!.classes()).toContain('grid-cols-[repeat(auto-fill,minmax(min(100%,17rem),1fr))]');
    expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(3);
    expect(wrapper.findAll('img').map((image) => image.attributes('src'))).toEqual([
      'https://cdn.example.test/curated.jpg',
    ]);
    expect(wrapper.findAll('h3 a').map((link) => link.attributes('href'))).toEqual([
      '/events/october',
      '/events/march',
      '/events/undated',
    ]);
  });

  it('renders no filter bar: no toolbar, no search box, no Filters button, no match pill', async () => {
    const { wrapper } = await render(
      { tracking: true, canPropose: true, kinds: [{ key: 'runway', label: { en: 'Runway' } }] },
      trackingRoutes,
    );

    expect(wrapper.find('[data-test="events-toolbar"]').exists()).toBe(false);
    expect(wrapper.find('input[type="search"]').exists()).toBe(false);
    expect(wrapper.find('[data-facet-match]').exists()).toBe(false);
    expect(wrapper.find('[data-facet-search]').exists()).toBe(false);
    expect(wrapper.findAll('button').some((button) => button.text().startsWith('Filters'))).toBe(false);
    // What the feed does render of its own: the actions row (sort under tracking, proposal, overflow menu).
    const actions = wrapper.get('[data-test="events-actions"]');
    expect(actions.find('[data-test="feed-sort"]').exists()).toBe(true);
    expect(actions.findAll('button').some((button) => button.text() === 'Propose an event')).toBe(true);
    expect(actions.find('[data-test="feed-overflow"]').exists()).toBe(true);
  });

  it("lists the catalog from the page's filter state, with no counts read and no sort", async () => {
    const { wrapper, requests, filters } = await render(
      {
        kinds: [{ key: 'runway', label: { en: 'Runway' } }],
        regions: [{ key: 'local', label: { en: 'Local' }, countries: ['UZ'] }],
      },
      page,
    );

    expect(requests.every((request) => !new URL(request.url).pathname.includes('/tracking'))).toBe(true);
    expect(wrapper.find('[data-test="feed-sort"]').exists()).toBe(false);
    expect(filters.matchCount.value).toBeUndefined();
    const facets = filters.facetDefinitions.value;
    expect(facets.map((facet) => facet.key)).toEqual(['scope', 'kind', 'when']);
    expect(facets.find((facet) => facet.key === 'kind')?.kind).toBe('single');
    expect(
      facets
        .flatMap((facet) => (facet.kind === 'toggle' ? [] : facet.options))
        .every((option) => option.count === undefined),
    ).toBe(true);
    expect(facets.some((facet) => facet.kind === 'toggle')).toBe(false);
    expect(wrapper.find('[data-test="attention-pills"]').exists()).toBe(false);
  });

  it("filters the catalog by kind, scope and date presets through the page's selection", async () => {
    const { requests, filters } = await render(
      {
        kinds: [{ key: 'runway', label: { en: 'Runway' } }],
        regions: [{ key: 'local', label: { en: 'Local' }, countries: ['UZ'] }],
      },
      page,
    );
    const params = () => new URL(requests.at(-1)!.url).searchParams;

    filters.selection.value = { kind: 'runway', scope: 'local', when: 'thisYear' };
    await flushPromises();

    expect(params().get('kind')).toBe('runway');
    expect(params().get('country')).toBe('UZ');
    expect(params().get('from')).toBe('2026-01-01');
    expect(params().get('to')).toBe('2026-12-31');
    expect(params().get('includeUndated')).toBeNull();
  });

  it('scrolls through the catalog list with no button', async () => {
    const view = stubIntersectionObserver();
    const next = eventModel('next', {
      title: { und: 'Winter Market' },
      starts: { value: '2027-05-01', precision: 'day', certainty: 'announced' },
    });
    const { wrapper, requests } = await render({}, (request) =>
      new URL(request.url).searchParams.get('cursor') === 'c2'
        ? envelope({ items: [next], nextCursor: null })
        : envelope({ items: events, nextCursor: 'c2' }),
    );
    expect(wrapper.findAll('button').some((button) => /load more/i.test(button.text()))).toBe(false);

    view.fire();
    await flushPromises();

    expect(new URL(requests.at(-1)!.url).searchParams.get('cursor')).toBe('c2');
    expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(4);
  });

  it('opens an event from a card', async () => {
    const { wrapper } = await render({}, page);

    await wrapper.get('[data-test="event-media-card"] h3 button').trigger('click');

    expect(wrapper.emitted('open')).toEqual([['october']]);
  });

  it('sends a proposal for review and says it is not published yet', async () => {
    const candidate = eventModel('c1', { title: { und: 'Pop-up Show' } }, { review: 'candidate' });
    const { wrapper, requests } = await render({ canPropose: true }, page, () => envelope(candidate, { status: 201 }));

    [...wrapper.element.querySelectorAll('button')]
      .find((item) => item.textContent?.trim() === 'Propose an event')
      ?.click();
    await flushPromises();
    await wrapper.get('input[name="title"]').setValue('Pop-up Show');
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    const proposal = requests.find((request) => request.method === 'POST')!;
    expect(new URL(proposal.url).pathname).toBe('/api/events/v1/catalogs/ocharo/candidates');
    expect(await proposal.json()).toMatchObject({ facts: { title: { und: 'Pop-up Show' } } });
    expect(wrapper.get('[data-test="proposal-sent"]').text()).toContain('awaiting review');
    expect(wrapper.emitted('proposed')).toEqual([[candidate]]);
    expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(3);
  });

  it('shows a proposal the private catalog published at once, reloading the feed', async () => {
    const added = eventModel('c2', { title: { und: 'Pop-up Show' } }, { review: 'published' });
    let lists = 0;
    const { wrapper, requests } = await render({ canPropose: true }, (request) => {
      if (request.method === 'POST') return envelope(added, { status: 201 });
      lists += 1;
      return lists === 1 ? page() : envelope({ items: [added], nextCursor: null });
    });

    [...wrapper.element.querySelectorAll('button')]
      .find((item) => item.textContent?.trim() === 'Propose an event')
      ?.click();
    await flushPromises();
    await wrapper.get('input[name="title"]').setValue('Pop-up Show');
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    expect(requests.filter((request) => request.method === 'GET')).toHaveLength(2);
    expect(wrapper.get('[data-test="proposal-sent"]').text()).toContain('is in the list now');
    expect(wrapper.get('[data-test="proposal-sent"]').text()).not.toContain('awaiting review');
    expect(wrapper.emitted('proposed')).toEqual([[added]]);
    expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(1);
  });

  it('keeps a proposal with an unreadable date in the form', async () => {
    const { wrapper, requests } = await render({ canPropose: true }, page);

    [...wrapper.element.querySelectorAll('button')]
      .find((item) => item.textContent?.trim() === 'Propose an event')
      ?.click();
    await flushPromises();
    await wrapper.get('input[name="title"]').setValue('Show');
    await pick(wrapper.get('[data-test="starts-precision"]').element, 'Month');
    await wrapper.get('input[name="starts-value"]').setValue('14.03.2026');
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    expect(requests.some((request) => request.method === 'POST')).toBe(false);
    expect(wrapper.get('form [role="alert"]').text()).toContain('2026-03');
  });
});

describe('image source forwarded by the feed', () => {
  it('passes imageSrc to every card: its source first, the url on error, the date tile when both fail', async () => {
    const resolver = vi.fn(
      (event: EventModel, _image: EventImage, index: number) => `https://files.example.test/${event.id}-${index}`,
    );
    const { wrapper } = await render({ imageSrc: resolver }, page);

    expect(resolver).toHaveBeenCalledTimes(1);
    expect(resolver.mock.calls[0]![0].id).toBe('october');
    expect(resolver.mock.calls[0]![2]).toBe(0);
    expect(wrapper.get('img').attributes('src')).toBe('https://files.example.test/october-0');
    expect(wrapper.findAll('[data-test="event-date-tile"]')).toHaveLength(2);

    await wrapper.get('img').trigger('error');
    expect(wrapper.get('img').attributes('src')).toBe('https://cdn.example.test/curated.jpg');
    expect(wrapper.get('img').attributes('referrerpolicy')).toBe('no-referrer');

    await wrapper.get('img').trigger('error');
    expect(wrapper.find('img').exists()).toBe(false);
    expect(wrapper.findAll('[data-test="event-date-tile"]')).toHaveLength(3);
  });
});

describe('card slots forwarded by the feed', () => {
  /** Mounts the feed with `card-*` slots; `page` answers every list read. */
  async function renderCards(slots: Record<string, (scope: never) => unknown>, props: Record<string, unknown> = {}) {
    const mounted = await mountFeed(props, page, { slots });
    wrappers.push(mounted.host);
    return mounted.wrapper;
  }

  it('card-media replaces the curated image in every card and receives the image', async () => {
    const wrapper = await renderCards({
      'card-media': (scope: { event: { id: string }; image: { url: string } | null }) =>
        h('div', { 'data-test': 'custom-media' }, `${scope.event.id}:${scope.image?.url ?? 'none'}`),
    });

    expect(wrapper.findAll('[data-test="event-image"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="event-date-tile"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="custom-media"]').map((item) => item.text())).toEqual([
      'october:https://cdn.example.test/curated.jpg',
      'march:none',
      'undated:none',
    ]);
  });

  it('card-fallback replaces the date tile of the cards with no image and receives the tile', async () => {
    const wrapper = await renderCards({
      'card-fallback': (scope: { tile: { big: string }; kindKey?: string }) =>
        h('div', { 'data-test': 'custom-fallback' }, `${scope.tile.big}/${scope.kindKey}`),
    });

    expect(wrapper.findAll('[data-test="event-date-tile"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="custom-fallback"]').map((item) => item.text())).toEqual([
      '14/runway',
      '?/runway',
    ]);
    expect(wrapper.findAll('[data-test="event-image"]')).toHaveLength(1);
  });

  it('card-pills replaces the pills and receives the labels', async () => {
    const wrapper = await renderCards(
      {
        'card-pills': (scope: { kind: string; stage: string; online: boolean }) =>
          h('div', { 'data-test': 'custom-pills' }, `${scope.kind}|${scope.stage}|${scope.online}`),
      },
      { kinds: [{ key: 'runway', label: { en: 'Runway Show' } }] },
    );

    expect(wrapper.findAll('[data-test="event-pills"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="custom-pills"]').map((item) => item.text())).toEqual([
      'Runway Show||false',
      'Runway Show||false',
      'Runway Show||false',
    ]);
  });

  it('card-actions replaces the top-end controls and receives a track function', async () => {
    const wrapper = await renderCards({
      'card-actions': (scope: { event: { id: string }; highPriority: boolean; track: (stage: string) => void }) =>
        h(
          'button',
          { type: 'button', 'data-test': 'custom-action', onClick: () => scope.track(`stage-of-${scope.event.id}`) },
          String(scope.highPriority),
        ),
    });

    expect(wrapper.findAll('[data-test="custom-action"]')).toHaveLength(3);
    expect(wrapper.findAll('[data-test="event-stage-menu"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="event-priority"]')).toHaveLength(0);
    expect(wrapper.get('[data-test="custom-action"]').text()).toBe('false');
  });

  it('card-title replaces the heading content and receives the title, href and open', async () => {
    const wrapper = await renderCards(
      {
        'card-title': (scope: { title: string; href?: string; open: () => void }) =>
          h('span', { 'data-test': 'custom-title', onClick: scope.open }, `${scope.title} -> ${scope.href}`),
      },
      { eventHref: (id: string) => `/events/${id}` },
    );

    expect(wrapper.findAll('h3 a')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="custom-title"]').map((item) => item.text())).toEqual([
      'Fashion Week -> /events/october',
      'Spring Fair -> /events/march',
      'Someday Market -> /events/undated',
    ]);

    await wrapper.get('[data-test="custom-title"]').trigger('click');
    expect(wrapper.emitted('open')).toEqual([['october']]);
  });

  it('card-meta replaces the dates and city line and receives the dates, city and state', async () => {
    const wrapper = await renderCards({
      'card-meta': (scope: { dates: string; city: string; finished: boolean; ongoing: boolean }) =>
        h('p', { 'data-test': 'custom-meta' }, `${scope.dates}|${scope.city}|${scope.finished}|${scope.ongoing}`),
    });

    expect(wrapper.findAll('[data-test="event-dates"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="custom-meta"]').map((item) => item.text())).toEqual([
      '≈ Oct 2026 (expected)||true|false',
      '14–16 Mar 2027||false|false',
      'Dates not announced||false|false',
    ]);
  });

  it('card-detail replaces the deadline line and receives the deadline and sources', async () => {
    const wrapper = await renderCards({
      'card-detail': (scope: EventCardDetailSlot) =>
        h(
          'p',
          { 'data-test': 'custom-detail' },
          `${scope.deadline ? `${scope.deadline.label}/${scope.deadline.left}/${scope.deadline.tone}` : 'none'}|${scope.sources.length}`,
        ),
    });

    expect(wrapper.findAll('[data-test="event-deadline"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-test="custom-detail"]').map((item) => item.text())).toEqual([
      'none|0',
      'apply by 20 Nov/10d/urgent|0',
      'none|0',
    ]);
  });

  it('keeps a card zone whose slot the consumer left empty', async () => {
    const wrapper = await renderCards({
      'card-detail': () => h('p', { 'data-test': 'custom-detail' }, 'x'),
    });

    expect(wrapper.findAll('[data-test="event-dates"]')).toHaveLength(3);
    expect(wrapper.findAll('[data-test="event-title"]')).toHaveLength(3);
    expect(wrapper.findAll('[data-test="event-kind"]')).toHaveLength(3);
    expect(wrapper.findAll('[data-test="event-image"]')).toHaveLength(1);
  });
});
