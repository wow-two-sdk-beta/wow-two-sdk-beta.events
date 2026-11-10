import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import { EventsFeed } from '../src/kit';
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
