import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import CardGallery from '../src/presentation/gallery/CardGallery.vue';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
});

describe('event card gallery', () => {
  it('renders every sample state at the four widths', () => {
    const wrapper = mount(CardGallery);
    wrappers.push(wrapper);

    const frames = wrapper.findAll('[data-test="gallery-frame"]');
    expect(frames.map((frame) => frame.get('h2').text().split(' ')[0])).toEqual(['1440', '1024', '768', '390']);
    expect(frames.map((frame) => (frame.get('div').element as HTMLElement).style.width)).toEqual([
      '1392px',
      '976px',
      '720px',
      '342px',
    ]);
    const first = frames[0]!;
    expect(first.findAll('[data-test="event-media-card"]')).toHaveLength(10);
    // Every card shows its image or the date tile; the test build inlines the posters as data addresses, which a card
    // refuses, so only the dev server's http addresses show the images.
    expect(first.findAll('[data-test="event-image"], [data-test="event-date-tile"]')).toHaveLength(10);
    expect(first.findAll('[data-test="event-date-tile"]').length).toBeGreaterThanOrEqual(5);
    expect(first.findAll('[data-dimmed]')).toHaveLength(3);
    expect(first.find('[data-test="event-deadline"][data-tone="urgent"]').exists()).toBe(true);
    // A month-only start keeps the muted day placeholder, the short month and the year; the undated one is muted
    // throughout, with "TBA" for the month — every tile the same shape, no icon.
    const tiles = first.findAll('[data-test="event-date-tile"]');
    const monthOnly = tiles.filter(
      (tile) =>
        tile.get('[data-test="tile-day"]').text() === '?' && tile.get('[data-test="tile-month"]').text() !== 'TBA',
    );
    expect(monthOnly).toHaveLength(1);
    expect(monthOnly[0]!.get('[data-test="tile-month"]').text()).toBe('Apr');
    expect(monthOnly[0]!.get('[data-test="tile-year"]').text()).toBe('2027');
    expect(tiles.filter((tile) => tile.get('[data-test="tile-month"]').text() === 'TBA')).toHaveLength(1);
    expect(first.find('[data-test="event-date-tile"] svg').exists()).toBe(false);
    expect(first.find('[data-test="event-priority"]').exists()).toBe(true);
    expect(first.find('[data-test="event-stage-menu"]').exists()).toBe(true);
  });
});
