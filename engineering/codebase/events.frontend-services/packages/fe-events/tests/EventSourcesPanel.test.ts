import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import { EventSourcesPanel } from '../src/admin';
import type { EventModel } from '../src/contracts';
import { envelope, eventModel, fakeService, sourceModel } from './Fixtures';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
});

const joined = eventModel(
  'e1',
  { title: { und: 'TITF 2026' } },
  {
    revision: 4,
    sources: [
      sourceModel('afisha', { name: 'Afisha Uzbekistan' }),
      sourceModel('uzexpo', { name: 'Uzexpocentre', matchedBy: 'rule:title-date-city' }),
    ],
  },
);

function render(event: EventModel, ...responders: Parameters<typeof fakeService>) {
  const service = fakeService(...responders);
  const wrapper = mount(EventSourcesPanel, { props: { client: service.client, catalog: 'ocharo', event } });
  wrappers.push(wrapper);
  return { ...service, wrapper };
}

const button = (wrapper: ReturnType<typeof mount>, text: string) =>
  [...wrapper.element.querySelectorAll('button')].find((element) => element.textContent?.trim() === text);

describe('event sources panel', () => {
  it('names each source and marks the one a rule attached', () => {
    const { wrapper } = render(joined);

    const rows = wrapper.findAll('[data-test="event-source-row"]');
    expect(rows).toHaveLength(2);
    expect(rows[0]!.text()).toContain('Afisha Uzbekistan');
    expect(rows[0]!.find('[data-test="source-matched"]').exists()).toBe(false);
    expect(rows[1]!.text()).toContain('Uzexpocentre');
    expect(rows[1]!.get('[data-test="source-matched"]').text()).toBe('Joined by rule: same title, dates and city');
  });

  it('shows nothing for an event with one source', () => {
    const { wrapper } = render(eventModel('e2', {}, { sources: [sourceModel('afisha')] }));

    expect(wrapper.find('[data-test="event-sources"]').exists()).toBe(false);
  });

  it('splits only after a confirmation, with the source and the revision read, and tells the host', async () => {
    const candidate = eventModel(
      'e3',
      { title: { und: 'TITF' } },
      { review: 'candidate', sources: [joined.sources![1]!] },
    );
    const { wrapper, requests } = render(joined, () => envelope(candidate));

    button(wrapper, 'Split source')?.click();
    await flushPromises();
    expect(requests).toHaveLength(0);
    expect(wrapper.text()).toContain('never attached to this event again');

    button(wrapper, 'Confirm split')?.click();
    await flushPromises();

    expect(new URL(requests[0]!.url).pathname).toBe('/api/events/v1/catalogs/ocharo/review/e1/split');
    expect(await requests[0]!.json()).toEqual({ kind: 'scfd', sourceId: 'scfd/afisha', expectedRevision: 4 });
    expect(wrapper.emitted('split')).toEqual([[candidate]]);
  });

  it('offers no split for an import source, or on an event no longer in review', () => {
    const withImport = eventModel(
      'e4',
      {},
      { sources: [sourceModel('afisha'), sourceModel('legacy', { kind: 'import', id: 'ocharo-desk/12' })] },
    );
    const rejected = eventModel('e5', {}, { review: 'rejected', sources: joined.sources! });

    const { wrapper: imported } = render(withImport);
    const { wrapper: closed } = render(rejected);

    expect(imported.findAll('button')).toHaveLength(1);
    expect(imported.get('button').text()).toBe('Split source');
    expect(closed.findAll('button')).toHaveLength(0);
  });

  it('asks the host to reload when the event changed meanwhile', async () => {
    const { wrapper } = render(joined, () => Response.json({ title: 'Conflict' }, { status: 409 }));

    button(wrapper, 'Split source')?.click();
    await flushPromises();
    button(wrapper, 'Confirm split')?.click();
    await flushPromises();

    expect(wrapper.emitted('stale')).toHaveLength(1);
    expect(wrapper.find('[role="alert"]').exists()).toBe(true);
    expect(wrapper.emitted('split')).toBeUndefined();
  });
});
