import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import FeedGallery from '../src/presentation/gallery/FeedGallery.vue';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

/** Replaces `IntersectionObserver` with one that reports the sentinel in view only when `fire()` says so. */
function stubIntersectionObserver() {
  const live = new Set<{ callback: IntersectionObserverCallback; targets: Element[] }>();
  class FakeObserver {
    private readonly record: { callback: IntersectionObserverCallback; targets: Element[] };
    constructor(callback: IntersectionObserverCallback) {
      this.record = { callback, targets: [] };
      live.add(this.record);
    }
    observe(target: Element) {
      this.record.targets.push(target);
    }
    unobserve() {}
    disconnect() {
      live.delete(this.record);
    }
    takeRecords() {
      return [];
    }
  }
  vi.stubGlobal('IntersectionObserver', FakeObserver);
  return {
    fire() {
      for (const record of [...live]) {
        const entries = record.targets.map((target) => ({ isIntersecting: true, target }) as IntersectionObserverEntry);
        if (entries.length) record.callback(entries, record as unknown as IntersectionObserver);
      }
    },
  };
}

async function settle() {
  await new Promise((resolve) => setTimeout(resolve, 30));
  await flushPromises();
}

describe('events feed page', () => {
  it('renders the feed in tracking mode over the fake service, scrolls to the next page and expands both pills', async () => {
    const view = stubIntersectionObserver();
    const wrapper = mount(FeedGallery, { props: { latencyMs: 0 } });
    wrappers.push(wrapper);
    await settle();

    expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(12);
    expect(wrapper.get('[data-facet-match]').text()).toMatch(/^(\d+) of \1$/);
    expect(wrapper.findAll('button').some((button) => button.text() === 'Filters')).toBe(true);
    expect(wrapper.findAll('[data-test="event-group"] > h3').length).toBeGreaterThanOrEqual(2);
    expect(wrapper.findAll('[data-test="event-stage-menu"]').length).toBeGreaterThan(0);
    expect(wrapper.findAll('[data-test="attention-pills"] button').map((button) => button.text())).toEqual([
      '4 deadlines in 14 days',
      '2 past to close',
    ]);

    view.fire();
    await settle();
    expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(24);

    await wrapper.get('[data-pill="past"]').trigger('click');
    expect(wrapper.findAll('[data-test="past-strip"] li')).toHaveLength(2);
    await wrapper.get('[data-test="past-strip"] button').trigger('click');
    expect(wrapper.find('[data-test="past-strip"]').exists()).toBe(false);

    await wrapper.get('[data-pill="deadlines"]').trigger('click');
    await settle();
    expect(wrapper.findAll('[data-test="deadline"]').length).toBeGreaterThan(0);
  });

  it('moves a card to a stage through its menu and counts again', async () => {
    const wrapper = mount(FeedGallery, { props: { latencyMs: 0 }, attachTo: document.body });
    wrappers.push(wrapper);
    await settle();

    await wrapper.get('[data-test="event-stage-menu"]').trigger('click');
    await flushPromises();
    const entries = [...document.querySelectorAll<HTMLElement>('[data-test="event-quick-stage"]')];
    expect(entries.map((entry) => entry.textContent?.trim())).toEqual(['Shortlisted', 'Skipped']);
    entries[0]!.click();
    await settle();

    expect(wrapper.get('[data-test="feed-last"]').text()).toMatch(/^track ev-\d+ → shortlisted$/);
  });

  it("opens a card into its event's detail above the feed", async () => {
    const wrapper = mount(FeedGallery, { props: { latencyMs: 0 } });
    wrappers.push(wrapper);
    await settle();

    await wrapper.get('[data-test="event-media-card"] h3 a').trigger('click', { button: 0 });
    await settle();

    expect(wrapper.get('[data-test="feed-last"]').text()).toMatch(/^open ev-\d+$/);
    expect(wrapper.get('[data-test="feed-detail"] #event-detail-title').text()).not.toBe('');
    expect(wrapper.find('[data-test="feed-detail"] [data-test="event-scope"]').exists()).toBe(true);
    await wrapper.get('[data-test="feed-detail"] > button').trigger('click');
    expect(wrapper.find('[data-test="feed-detail"]').exists()).toBe(false);
  });
});
