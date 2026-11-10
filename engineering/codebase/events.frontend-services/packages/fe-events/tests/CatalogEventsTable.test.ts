import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CatalogEventsTable } from '../src/admin';
import { envelope, eventModel, fakeService } from './Fixtures';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
  vi.unstubAllGlobals();
});

async function render(...responders: Parameters<typeof fakeService>) {
  const service = fakeService(...responders);
  const wrapper = mount(CatalogEventsTable, { props: { client: service.client, catalog: 'ocharo' } });
  wrappers.push(wrapper);
  await flushPromises();
  return { ...service, wrapper };
}

describe('catalog events table', () => {
  it('shows who auto-accepted an event and why', async () => {
    const event = eventModel('e1', {}, {
      reviewedBy: 'classifier:ocharo-fashion-v1',
      reviewReason: 'passed SCFD filter ocharo-fashion-v1',
    });
    const { wrapper } = await render(() => envelope({ items: [event], nextCursor: null }));

    const note = wrapper.get('[data-test="review-attribution"]').text();
    expect(note).toContain('Auto-accepted by ocharo-fashion-v1');
    expect(note).toContain('passed SCFD filter ocharo-fashion-v1');
  });

  it('shows the reason a candidate was held', async () => {
    const event = eventModel('e2', {}, { review: 'candidate', reviewReason: 'held: irrelevant (0.92): not a fashion event' });
    const { wrapper } = await render(() => envelope({ items: [event], nextCursor: null }));

    expect(wrapper.get('[data-test="review-attribution"]').text()).toContain('not a fashion event');
  });

  it('returns a published event to review after a confirmation', async () => {
    const confirm = vi.fn(() => true);
    vi.stubGlobal('confirm', confirm);
    const event = eventModel('e3', {}, { revision: 4 });
    const { wrapper, requests } = await render(
      () => envelope({ items: [event], nextCursor: null }),
      () => envelope({ ...event, review: 'candidate', revision: 5 }),
      () => envelope({ items: [{ ...event, review: 'candidate', revision: 5 }], nextCursor: null }),
    );

    const button = [...wrapper.element.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Return to review');
    button?.click();
    await flushPromises();

    expect(confirm).toHaveBeenCalledOnce();
    expect(requests[1]!.url).toContain('/review/e3/reopen?expectedRevision=4');
    expect(requests).toHaveLength(3);
  });
});
