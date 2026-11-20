import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import { EventCard, EventMediaCard, EventsFeed } from '../src/kit';
import { envelope, eventModel, fakeService } from './Fixtures';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
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

async function render(props: Record<string, unknown>, ...responders: Parameters<typeof fakeService>) {
  const service = fakeService(...responders);
  const wrapper = mount(EventsFeed, {
    props: { client: service.client, catalog: 'ocharo', today: '2026-11-10', ...props },
  });
  wrappers.push(wrapper);
  await flushPromises();
  return { ...service, wrapper };
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
    expect(wrapper.get('[data-test="event-deadline"]').text()).toContain('10 days left');
  });

  it('shows curated images only and writes in the chosen language', async () => {
    const { wrapper } = await render({ locale: 'ru' }, page);

    expect(wrapper.findAll('img').map((image) => image.attributes('src'))).toEqual([
      'https://cdn.example.test/curated.jpg',
    ]);
    expect(wrapper.text()).toContain('Неделя моды');
    expect(wrapper.text()).toContain('≈ окт. 2026 (ожидается)');
  });

  it('lists events as EventCard rows by default', async () => {
    const { wrapper } = await render({}, page);

    expect(wrapper.findAllComponents(EventCard)).toHaveLength(3);
    expect(wrapper.findAllComponents(EventMediaCard)).toHaveLength(0);
    const lists = wrapper.findAll('[data-test="event-group"] > ul');
    expect(lists).toHaveLength(3);
    expect(lists.every((list) => list.classes().join(' ') === 'grid gap-3')).toBe(true);
    expect(wrapper.find('[data-test="event-cards"]').exists()).toBe(false);
  });

  it('lists events as image-first cards in a grid with variant "cards", keeping the month headers', async () => {
    const { wrapper } = await render({ variant: 'cards', eventHref: (id: string) => `/events/${id}` }, page);

    expect(wrapper.findAll('[data-test="event-group"] > h3').map((heading) => heading.text())).toEqual([
      'October 2026',
      'March 2027',
      'Date not announced',
    ]);
    expect(wrapper.findAllComponents(EventCard)).toHaveLength(0);
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

  it('opens an event from a card', async () => {
    const { wrapper } = await render({ variant: 'cards' }, page);

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
    expect(wrapper.findAll('[data-test="event-card"]')).toHaveLength(3);
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
    expect(wrapper.findAll('[data-test="event-card"]')).toHaveLength(1);
  });

  it('keeps a proposal with an unreadable date in the form', async () => {
    const { wrapper, requests } = await render({ canPropose: true }, page);

    [...wrapper.element.querySelectorAll('button')]
      .find((item) => item.textContent?.trim() === 'Propose an event')
      ?.click();
    await flushPromises();
    await wrapper.get('input[name="title"]').setValue('Show');
    await wrapper.get('select[name="starts-precision"]').setValue('month');
    await wrapper.get('input[name="starts-value"]').setValue('14.03.2026');
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    expect(requests.some((request) => request.method === 'POST')).toBe(false);
    expect(wrapper.get('form [role="alert"]').text()).toContain('2026-03');
  });
});
