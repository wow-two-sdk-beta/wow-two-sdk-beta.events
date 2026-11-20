import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import type { CatalogKindModel, EventFacts, EventModel, TrackingModel, WorkflowStageModel } from '../src/contracts';
import { EventMediaCard } from '../src/kit';
import { eventModel, sourceModel } from './Fixtures';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
  document.body.innerHTML = '';
});

const kinds: CatalogKindModel[] = [{ key: 'runway', label: { en: 'Runway Show', ru: 'Показ мод' } }];
const stages: WorkflowStageModel[] = [
  { key: 'candidate', label: { en: 'Candidate' }, class: 'open' },
  { key: 'shortlisted', label: { en: 'Shortlisted' }, class: 'active' },
  { key: 'skipped', label: { en: 'Skipped' }, class: 'lost' },
];
const tracking = (stage: string, extra: Partial<TrackingModel> = {}): TrackingModel => ({
  id: 'track-1',
  eventId: 'fashion',
  stage,
  priority: 'normal',
  revision: 1,
  createdAtUtc: '2026-11-01T10:00:00Z',
  updatedAtUtc: '2026-11-02T10:00:00Z',
  ...extra,
});
const triage = { stages, defaultStage: 'candidate', quickStages: ['shortlisted', 'skipped'], canTrack: true };
const poster = { url: 'https://cdn.example.test/poster.jpg', attribution: 'Ocharo' };

function build(facts: Partial<EventFacts> = {}, overrides: Partial<EventModel> = {}) {
  return eventModel(
    'fashion',
    {
      title: { ru: 'Неделя моды', und: 'Fashion Week' },
      starts: { value: '2026-12-12', precision: 'day', certainty: 'announced' },
      ends: { value: '2026-12-14', precision: 'day', certainty: 'announced' },
      place: { city: 'Tashkent', country: 'UZ' },
      ...facts,
    },
    overrides,
  );
}

function render(event: EventModel, props: Record<string, unknown> = {}) {
  const wrapper = mount(EventMediaCard, {
    props: { event, today: '2026-11-10', kinds, ...props },
    attachTo: document.body,
  });
  wrappers.push(wrapper);
  return wrapper;
}

const pills = (wrapper: ReturnType<typeof render>) =>
  wrapper.findAll('[data-test="event-pills"] > span').map((pill) => pill.text());

describe('event media card', () => {
  it('shows an untracked event with its kind pill and no stage, "new" or menu', () => {
    const wrapper = render(build());

    expect(pills(wrapper)).toEqual(['runway show']);
    expect(wrapper.find('[data-test="event-stage"]').exists()).toBe(false);
    expect(wrapper.text()).not.toContain('New');
    expect(wrapper.find('[data-test="event-stage-menu"]').exists()).toBe(false);
    expect(wrapper.attributes('data-dimmed')).toBeUndefined();
  });

  it('shows the stage pill, lower-case, once the event is tracked', () => {
    const wrapper = render(build(), { stages, tracking: tracking('shortlisted') });

    expect(pills(wrapper)).toEqual(['runway show', 'shortlisted']);
  });

  it('shows the pill, dims the card and strikes nothing for a cancelled event', () => {
    const wrapper = render(build({ status: 'cancelled' }));

    expect(wrapper.get('[data-test="event-status"]').text()).toBe('cancelled');
    expect(wrapper.attributes('data-dimmed')).toBeDefined();
  });

  it('names a postponed or online event by a pill without dimming it', () => {
    const postponed = render(build({ status: 'postponed', attendance: 'online' }));

    expect(pills(postponed)).toEqual(['runway show', 'postponed', 'online']);
    expect(postponed.attributes('data-dimmed')).toBeUndefined();
    expect(
      render(build({ status: 'movedOnline' }))
        .find('[data-test="event-online"]')
        .exists(),
    ).toBe(true);
  });

  it('shows a removed pill and dims an event whose listing left every source', () => {
    const wrapper = render(build(), { event: build({}, { sources: [sourceModel('a', { state: 'gone' })] }) });

    expect(wrapper.get('[data-test="event-removed"]').text()).toBe('removed');
    expect(wrapper.attributes('data-dimmed')).toBeDefined();
  });

  it('keeps an event with one live source undimmed', () => {
    const wrapper = render(build({}, { sources: [sourceModel('a', { state: 'gone' }), sourceModel('b')] }));

    expect(wrapper.find('[data-test="event-removed"]').exists()).toBe(false);
    expect(wrapper.attributes('data-dimmed')).toBeUndefined();
  });

  it('dims a past event and says finished', () => {
    const wrapper = render(build(), { today: '2026-12-20' });

    expect(wrapper.attributes('data-dimmed')).toBeDefined();
    expect(wrapper.get('[data-test="event-finished"]').text()).toBe('finished');
  });

  it('says ongoing while the dates cover today, and never for a month estimate', () => {
    expect(render(build(), { today: '2026-12-13' }).get('[data-test="event-ongoing"]').text()).toBe('ongoing');
    expect(render(build(), { today: '2026-12-13', locale: 'ru' }).get('[data-test="event-ongoing"]').text()).toBe(
      'идёт сейчас',
    );
    const estimate = build({
      starts: { value: '2026-11', precision: 'month', certainty: 'estimated' },
      ends: undefined,
    });
    expect(render(estimate).find('[data-test="event-ongoing"]').exists()).toBe(false);
  });

  it('fills the card with the curated image, lazily and without a referrer', () => {
    const wrapper = render(build({ images: [poster, { url: 'https://cdn.example.test/second.jpg' }] }));

    expect(wrapper.findAll('img')).toHaveLength(1);
    expect(wrapper.get('img').attributes()).toMatchObject({
      src: 'https://cdn.example.test/poster.jpg',
      loading: 'lazy',
      referrerpolicy: 'no-referrer',
      alt: 'Image: Ocharo',
    });
    expect(wrapper.get('img').classes()).toContain('object-cover');
    expect(wrapper.find('[data-test="event-date-tile"]').exists()).toBe(false);
    expect(wrapper.find('[data-test="event-carousel"]').exists()).toBe(false);
    expect(wrapper.find('[data-dot]').exists()).toBe(false);
  });

  it('names an image without attribution by the title', () => {
    const wrapper = render(build({ images: [{ url: 'https://cdn.example.test/plain.jpg' }] }));

    expect(wrapper.get('img').attributes('alt')).toBe('Fashion Week');
  });

  it('shows the date tile with the day, the month and the kind icon when there is no image', () => {
    const wrapper = render(build());

    expect(wrapper.find('img').exists()).toBe(false);
    const tile = wrapper.get('[data-test="event-date-tile"]');
    expect(tile.get('[data-test="tile-day"]').text()).toBe('12');
    expect(tile.get('[data-test="tile-month"]').text()).toBe('Dec');
    expect(tile.get('svg').attributes('data-glyph')).toBe('shirt');
    expect(tile.classes().join(' ')).toContain('from-primary/30');
  });

  it('writes the tile in the chosen language', () => {
    const wrapper = render(build({ starts: { value: '2027-03-14', precision: 'day', certainty: 'announced' } }), {
      locale: 'ru',
    });

    expect(wrapper.get('[data-test="tile-day"]').text()).toBe('14');
    expect(wrapper.get('[data-test="tile-month"]').text()).toBe('мар.');
  });

  it('shows the month and year on the tile for a month estimate, and the icon alone for an undated event', () => {
    const month = render(build({ starts: { value: '2027-03', precision: 'month', certainty: 'estimated' } }));
    expect(month.get('[data-test="tile-day"]').text()).toBe('Mar');
    expect(month.get('[data-test="tile-year"]').text()).toBe('2027');

    const undated = render(
      build({ starts: { value: null, precision: 'unknown', certainty: 'announced' }, kind: 'other' }),
    );
    expect(undated.find('[data-test="tile-day"]').exists()).toBe(false);
    expect(undated.get('[data-test="event-date-tile"] svg').attributes('data-glyph')).toBe('calendar');
  });

  it('falls back to the date tile when the image fails to load, or its address is unsafe', async () => {
    const failing = render(build({ images: [poster] }));
    await failing.get('img').trigger('error');
    expect(failing.find('img').exists()).toBe(false);
    expect(failing.find('[data-test="event-date-tile"]').exists()).toBe(true);

    expect(
      render(build({ images: [{ url: 'javascript:alert(1)' }] }))
        .find('img')
        .exists(),
    ).toBe(false);
  });

  it('writes the title in the chosen language and clamps a long one to two lines', () => {
    expect(render(build()).get('h3').text()).toBe('Fashion Week');
    expect(render(build(), { locale: 'ru' }).get('h3').text()).toBe('Неделя моды');

    const long = 'The International Central Asia Fashion and Textile Week with its Designers Showroom and Buyers Forum';
    const wrapper = render(build({ title: { und: long } }));
    expect(wrapper.get('h3').text()).toBe(long);
    expect(wrapper.get('h3').classes()).toContain('line-clamp-2');
  });

  it('writes the dates and the city on one line', () => {
    const wrapper = render(build());

    expect(wrapper.get('[data-test="event-dates"]').text()).toBe('12–14 Dec 2026');
    expect(wrapper.get('[data-test="event-city"]').text()).toBe('Tashkent');
  });

  it('shows the next open deadline short, in the urgent tone', () => {
    const urgent = render(
      build({
        deadlines: [{ kind: 'application', at: { value: '2026-11-15', precision: 'day', certainty: 'announced' } }],
      }),
    );
    const line = urgent.get('[data-test="event-deadline"]');
    expect(line.findAll('span').map((part) => part.text())).toEqual(['apply by 15 Nov', '5d']);
    expect(line.attributes('data-tone')).toBe('urgent');
    expect(line.classes()).toContain('text-red-300');
  });

  it('names another deadline kind before "by", and keeps a later one calm', () => {
    const calm = render(
      build({
        deadlines: [{ kind: 'registration', at: { value: '2027-01-20', precision: 'day', certainty: 'announced' } }],
      }),
    );
    const line = calm.get('[data-test="event-deadline"]');
    expect(line.findAll('span').map((part) => part.text())).toEqual(['registration by 20 Jan', '71d']);
    expect(line.attributes('data-tone')).toBe('upcoming');

    const early = render(
      build({
        deadlines: [{ kind: 'earlyBird', at: { value: '2026-11-30', precision: 'day', certainty: 'announced' } }],
      }),
    );
    expect(early.get('[data-test="event-deadline"] span').text()).toBe('early bird by 30 Nov');
  });

  it('reads today as "today" and tomorrow as 1d', () => {
    const at = (value: string) =>
      render(build({ deadlines: [{ kind: 'application', at: { value, precision: 'day', certainty: 'announced' } }] }));

    expect(at('2026-11-10').findAll('[data-test="event-deadline"] span')[1]!.text()).toBe('today');
    expect(at('2026-11-11').findAll('[data-test="event-deadline"] span')[1]!.text()).toBe('1d');
  });

  it('adds the year only to a deadline 12 or more months away', () => {
    const label = (value: string) =>
      render(build({ deadlines: [{ kind: 'application', at: { value, precision: 'day', certainty: 'announced' } }] }))
        .get('[data-test="event-deadline"] span')
        .text();

    expect(label('2027-11-09')).toBe('apply by 9 Nov');
    expect(label('2027-11-10')).toBe('apply by 10 Nov 2027');
    expect(label('2028-02-03')).toBe('apply by 3 Feb 2028');
    expect(label('2026-12-31')).toBe('apply by 31 Dec');
  });

  it('writes the short deadline in the chosen language', () => {
    const facts = {
      deadlines: [
        {
          kind: 'registration' as const,
          at: { value: '2026-11-15', precision: 'day', certainty: 'announced' } as const,
        },
      ],
    };
    const ru = render(build(facts), { locale: 'ru' }).findAll('[data-test="event-deadline"] span');
    expect(ru.map((part) => part.text())).toEqual(['регистрация до 15 нояб.', '5 дн.']);
    const uz = render(build(facts), { locale: 'uz' }).findAll('[data-test="event-deadline"] span');
    expect(uz.map((part) => part.text())).toEqual(['ro‘yxatdan o‘tish 15 noy gacha', '5 kun']);
  });

  it('shows a deadline known only to its month without a day count', () => {
    const wrapper = render(
      build({
        deadlines: [{ kind: 'application', at: { value: '2026-12', precision: 'month', certainty: 'announced' } }],
      }),
    );

    const parts = wrapper.findAll('[data-test="event-deadline"] span');
    expect(parts.map((part) => part.text())).toEqual(['apply by Dec 2026']);
  });

  it('skips a deadline that has passed and names the sources instead', () => {
    const wrapper = render(
      build(
        { deadlines: [{ kind: 'application', at: { value: '2026-11-01', precision: 'day', certainty: 'announced' } }] },
        { sources: [sourceModel('a', { name: 'Afisha' }), sourceModel('b', { name: 'UzExpoCentre' })] },
      ),
    );

    expect(wrapper.find('[data-test="event-deadline"]').exists()).toBe(false);
    expect(wrapper.get('[data-test="event-sources"]').text()).toBe('afisha · uzexpocentre');
  });

  it('shows the deadline in place of the sources, never both', () => {
    const wrapper = render(
      build(
        { deadlines: [{ kind: 'application', at: { value: '2026-11-15', precision: 'day', certainty: 'announced' } }] },
        { sources: [sourceModel('a', { name: 'Afisha' })] },
      ),
    );

    expect(wrapper.find('[data-test="event-deadline"]').exists()).toBe(true);
    expect(wrapper.find('[data-test="event-sources"]').exists()).toBe(false);
  });

  it('keeps organizer, scope, cost, the review state and the language tag off the card', () => {
    const wrapper = render(
      build(
        {
          organizer: { name: 'Style Agency' },
          offers: [{ kind: 'ticket', minPrice: 150000, currency: 'UZS' }],
          title: { ru: 'Неделя моды' },
        },
        { review: 'candidate' },
      ),
      { tracking: tracking('shortlisted', { fields: { cost: '500 USD' } }), stages },
    );

    const text = wrapper.text();
    for (const absent of ['500 USD', 'Style Agency', 'Cost', 'Tickets', 'Awaiting review', 'Local', 'RU']) {
      expect(text).not.toContain(absent);
    }
    expect(wrapper.find('[data-test="event-scope"]').exists()).toBe(false);
  });

  it('marks a high priority with a labelled icon, and nothing for a normal one', () => {
    const high = render(build(), { tracking: tracking('shortlisted', { priority: 'high' }), stages });
    const mark = high.get('[data-test="event-priority"]');
    expect(mark.attributes('aria-label')).toBe('High priority');
    expect(mark.attributes('role')).toBe('img');

    expect(
      render(build(), { tracking: tracking('shortlisted'), stages })
        .find('[data-test="event-priority"]')
        .exists(),
    ).toBe(false);
  });

  it('opens the event from a plain click on the title link, and leaves a modified click to the browser', () => {
    const wrapper = render(build(), { href: '/events/fashion' });
    const link = wrapper.get('h3 a');
    expect(link.attributes('href')).toBe('/events/fashion');
    expect(link.attributes('data-card-link')).toBeDefined();

    const plain = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    link.element.dispatchEvent(plain);
    expect(plain.defaultPrevented).toBe(true);
    expect(wrapper.emitted('open')).toEqual([['fashion']]);

    for (const modifier of [{ metaKey: true }, { ctrlKey: true }, { shiftKey: true }, { altKey: true }]) {
      const modified = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, ...modifier });
      link.element.dispatchEvent(modified);
      expect(modified.defaultPrevented).toBe(false);
    }
    expect(wrapper.emitted('open')).toHaveLength(1);
  });

  it('opens the event from a button when there is no page link', async () => {
    const wrapper = render(build());

    expect(wrapper.find('h3 a').exists()).toBe(false);
    const button = wrapper.get('h3 button');
    expect(button.attributes('data-card-link')).toBeDefined();
    await button.trigger('click');

    expect(wrapper.emitted('open')).toEqual([['fashion']]);
  });

  it('offers the quick stages in a menu that emits track and does not open the card', async () => {
    const wrapper = render(build(), { ...triage, quickStages: ['shortlisted', 'skipped', 'candidate'] });

    const trigger = wrapper.get('[data-test="event-stage-menu"]');
    expect(trigger.attributes('aria-label')).toBe('Stage');
    expect(document.querySelector('[data-test="event-quick-stage"]')).toBeNull();
    await trigger.trigger('click');
    await flushPromises();

    const items = [...document.querySelectorAll<HTMLElement>('[data-test="event-quick-stage"]')];
    expect(items.map((item) => item.textContent?.trim())).toEqual(['Shortlisted', 'Skipped']);
    items[0]!.click();
    await flushPromises();

    expect(wrapper.emitted('track')).toEqual([['fashion', 'shortlisted']]);
    expect(wrapper.emitted('open')).toBeUndefined();
  });

  it('offers the menu to a tracked event while it is still at the default stage', () => {
    const wrapper = render(build(), { ...triage, tracking: tracking('candidate') });

    expect(wrapper.find('[data-test="event-stage-menu"]').exists()).toBe(true);
    expect(wrapper.get('[data-test="event-stage"]').text()).toBe('candidate');
  });

  it('shows no menu without the right to track, or once the event left the default stage', () => {
    expect(
      render(build(), { ...triage, canTrack: false })
        .find('[data-test="event-stage-menu"]')
        .exists(),
    ).toBe(false);
    expect(
      render(build(), { ...triage, tracking: tracking('shortlisted') })
        .find('[data-test="event-stage-menu"]')
        .exists(),
    ).toBe(false);
    expect(
      render(build(), { quickStages: ['shortlisted'], canTrack: true })
        .find('[data-test="event-stage-menu"]')
        .exists(),
    ).toBe(false);
  });

  it('disables the menu while a write runs', () => {
    const wrapper = render(build(), { ...triage, trackPending: true });

    expect(wrapper.get('[data-test="event-stage-menu"]').attributes('disabled')).toBeDefined();
  });
});
