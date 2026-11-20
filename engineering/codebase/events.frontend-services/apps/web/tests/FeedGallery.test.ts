import { mount } from '@vue/test-utils';
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
    /** How many observers watch an element now: the list arms one after each page lands. */
    watching: () => [...live].filter((record) => record.targets.length > 0).length,
    fire() {
      for (const record of [...live]) {
        const entries = record.targets.map((target) => ({ isIntersecting: true, target }) as IntersectionObserverEntry);
        if (entries.length) record.callback(entries, record as unknown as IntersectionObserver);
      }
    },
  };
}

/**
 * Waits until the assertions hold. The fake service answers on a timer and each write or read is several hops long, so
 * a fixed pause races on a slow machine; the assertions stay exactly as strict, they are only retried until they pass.
 */
const until = (assertions: () => void) => vi.waitFor(assertions, { timeout: 5000, interval: 10 });

describe('events feed page', () => {
  it('renders the feed in tracking mode over the fake service, scrolls to the next page and expands both pills', async () => {
    const view = stubIntersectionObserver();
    const wrapper = mount(FeedGallery, { props: { latencyMs: 0 } });
    wrappers.push(wrapper);

    await until(() => {
      expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(12);
      expect(wrapper.get('[data-facet-match]').text()).toMatch(/^(\d+) of \1$/);
      expect(wrapper.findAll('button').some((button) => button.text() === 'Filters')).toBe(true);
      expect(wrapper.findAll('[data-test="event-group"] > h3').length).toBeGreaterThanOrEqual(2);
      expect(wrapper.findAll('[data-test="event-stage-menu"]').length).toBeGreaterThan(0);
      expect(wrapper.findAll('[data-test="attention-pills"] button').map((button) => button.text())).toEqual([
        '4 deadlines in 14 days',
        '2 past to close',
      ]);
      expect(view.watching()).toBe(1);
    });

    view.fire();
    await until(() => expect(wrapper.findAll('[data-test="event-media-card"]')).toHaveLength(24));

    await wrapper.get('[data-pill="past"]').trigger('click');
    expect(wrapper.findAll('[data-test="past-strip"] li')).toHaveLength(2);
    await wrapper.get('[data-test="past-strip"] button').trigger('click');
    expect(wrapper.find('[data-test="past-strip"]').exists()).toBe(false);

    await wrapper.get('[data-pill="deadlines"]').trigger('click');
    await until(() => expect(wrapper.findAll('[data-test="deadline"]').length).toBeGreaterThan(0));
  });

  it('moves a card to a stage through its menu and counts again', async () => {
    const wrapper = mount(FeedGallery, { props: { latencyMs: 0 }, attachTo: document.body });
    wrappers.push(wrapper);
    await until(() => expect(wrapper.findAll('[data-test="event-stage-menu"]').length).toBeGreaterThan(0));

    await wrapper.get('[data-test="event-stage-menu"]').trigger('click');
    const quickStages = () => [...document.querySelectorAll<HTMLElement>('[data-test="event-quick-stage"]')];
    await until(() =>
      expect(quickStages().map((entry) => entry.textContent?.trim())).toEqual(['Shortlisted', 'Skipped']),
    );
    quickStages()[0]!.click();

    await until(() => expect(wrapper.get('[data-test="feed-last"]').text()).toMatch(/^track ev-\d+ → shortlisted$/));
  });

  it("opens a card into its event's detail above the feed", async () => {
    const wrapper = mount(FeedGallery, { props: { latencyMs: 0 } });
    wrappers.push(wrapper);
    await until(() => expect(wrapper.find('[data-test="event-media-card"] h3 a').exists()).toBe(true));

    await wrapper.get('[data-test="event-media-card"] h3 a').trigger('click', { button: 0 });

    await until(() => {
      expect(wrapper.get('[data-test="feed-last"]').text()).toMatch(/^open ev-\d+$/);
      expect(wrapper.get('[data-test="feed-detail"] #event-detail-title').text()).not.toBe('');
      expect(wrapper.find('[data-test="feed-detail"] [data-test="event-scope"]').exists()).toBe(true);
    });
    await wrapper.get('[data-test="feed-detail"] > button').trigger('click');
    expect(wrapper.find('[data-test="feed-detail"]').exists()).toBe(false);
  });
});
