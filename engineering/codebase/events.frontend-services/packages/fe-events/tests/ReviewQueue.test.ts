import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import { ReviewQueue } from '../src/admin';
import type { ReviewItemModel } from '../src/contracts';
import { envelope, eventModel, fakeService, observationModel } from './Fixtures';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
});

const candidate: ReviewItemModel = {
  event: eventModel(
    'candidate-1',
    {
      title: { und: 'Tashkent Fashion Week' },
      starts: { value: '2026-10', precision: 'month', certainty: 'estimated' },
    },
    { review: 'candidate', revision: 1 },
  ),
  observations: [observationModel('o1')],
};
const published: ReviewItemModel = {
  event: eventModel(
    'published-1',
    { title: { und: 'Bishkek Expo' }, starts: { value: '2026-09-01', precision: 'day', certainty: 'announced' } },
    { pendingChanges: 1, revision: 5 },
  ),
  observations: [
    observationModel('o2', {
      state: 'pending',
      eventId: 'published-1',
      proposed: {
        title: { und: 'Bishkek Expo' },
        kind: 'runway',
        status: 'scheduled',
        starts: { value: '2026-09-03', precision: 'day', certainty: 'announced' },
        images: [{ url: 'https://afisha.example.test/poster.jpg' }],
      },
    }),
  ],
};
const queue =
  (...items: ReviewItemModel[]) =>
  () =>
    envelope({ items, nextCursor: null });

async function render(...responders: Parameters<typeof fakeService>) {
  const service = fakeService(...responders);
  const wrapper = mount(ReviewQueue, { props: { client: service.client, catalog: 'ocharo' } });
  wrappers.push(wrapper);
  await flushPromises();
  return { ...service, wrapper };
}

const button = (wrapper: ReturnType<typeof mount>, text: string, within = wrapper.element) =>
  [...within.querySelectorAll('button')].find((element) => element.textContent?.trim() === text);

describe('review queue', () => {
  it('shows candidates and pending changes with their evidence, sourced media only as text', async () => {
    const { wrapper, requests } = await render(queue(candidate, published));

    expect(new URL(requests[0]!.url).pathname).toBe('/api/events/v1/catalogs/ocharo/review');
    const items = wrapper.findAll('[data-test="review-item"]');
    expect(items).toHaveLength(2);
    expect(items[0]!.text()).toContain('Tashkent Fashion Week');
    expect(items[0]!.text()).toContain('≈ Oct 2026 (expected)');
    expect(items[0]!.text()).toContain('capture-o1');
    expect(items[0]!.get('[data-test="observation-links"] a').attributes('href')).toBe(
      'https://afisha.example.test/fashion-week',
    );
    expect(items[0]!.text()).toContain('Source evidence');
    expect(items[1]!.get('[data-test="pending-change"]').text()).toContain('3 Sep 2026');
    expect(items[1]!.text()).toContain('https://afisha.example.test/poster.jpg');
    expect(wrapper.find('img').exists()).toBe(false);
  });

  it('accepts a candidate at its revision, then reloads the queue', async () => {
    const { wrapper, requests } = await render(
      queue(candidate),
      () => envelope({ ...candidate.event, review: 'published', revision: 2 }),
      queue(),
    );

    button(wrapper, 'Accept')?.click();
    await flushPromises();

    expect(requests[1]!.method).toBe('POST');
    expect(new URL(requests[1]!.url).pathname).toBe('/api/events/v1/catalogs/ocharo/review/candidate-1/accept');
    expect(await requests[1]!.json()).toEqual({ expectedRevision: 1 });
    expect(wrapper.get('[role="status"]').text()).toBe('Accepted and published.');
    expect(wrapper.text()).toContain('Nothing to review.');
  });

  it('rejects only after a confirmation', async () => {
    const { wrapper, requests } = await render(
      queue(candidate),
      () => envelope({ ...candidate.event, review: 'rejected', revision: 2 }),
      queue(),
    );

    button(wrapper, 'Reject')?.click();
    await flushPromises();
    expect(requests).toHaveLength(1);

    button(wrapper, 'Confirm reject')?.click();
    await flushPromises();
    expect(requests[1]!.url).toContain('/review/candidate-1/reject?expectedRevision=1');
  });

  it('applies and dismisses a pending change', async () => {
    const { wrapper, requests } = await render(
      queue(published),
      () => envelope({ ...published.event, revision: 6 }),
      queue(published),
      () => envelope({ ...published.observations![0]!, state: 'dismissed' }),
      queue(),
    );

    button(wrapper, 'Apply')?.click();
    await flushPromises();
    expect(new URL(requests[1]!.url).pathname).toBe('/api/events/v1/catalogs/ocharo/observations/o2/apply');
    expect(await requests[1]!.json()).toEqual({ expectedRevision: 5 });

    button(wrapper, 'Dismiss')?.click();
    await flushPromises();
    expect(new URL(requests[3]!.url).pathname).toBe('/api/events/v1/catalogs/ocharo/observations/o2/dismiss');
  });

  it('says which permission is missing on a 403', async () => {
    const { wrapper } = await render(() => Response.json({ title: 'Forbidden' }, { status: 403 }));

    expect(wrapper.get('[role="alert"]').text()).toContain('events:review');
  });
});
