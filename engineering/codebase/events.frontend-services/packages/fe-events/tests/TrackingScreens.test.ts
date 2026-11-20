import { flushPromises, mount } from '@vue/test-utils';
import { CountryPicker } from '@wow-two-beta/fe/presentation/forms';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { EventImage, EventModel, TrackingModel, WorkflowModel } from '../src/contracts';
import {
  distinctEventImages,
  eventFactsDraft,
  EventDetail,
  EventFactsFields,
  EventProposalForm,
  EventsDeadlines,
  languageName,
  TrackingForm,
} from '../src/kit';
import { envelope, eventModel, fakeService } from './Fixtures';
import { columnsAt, desktop, heightAt, maxWidthAt, phone, utilitiesAt } from './Viewport';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
});

const workflow: WorkflowModel = {
  stages: [
    { key: 'candidate', label: { en: 'Candidate' } },
    { key: 'shortlisted', label: { en: 'Shortlisted' } },
  ],
  defaultStage: 'candidate',
  fields: {
    type: 'object',
    properties: {
      cost: { type: 'string', title: { en: "Ocharo's cost" }, maxLength: 500 },
      sourceUrl: { type: 'string', title: { en: 'Source' }, readOnly: true },
      internal: { type: 'string', title: { en: 'Internal' } },
    },
  },
  revision: 1,
};
const tracking = (extra: Partial<TrackingModel> = {}): TrackingModel => ({
  id: 't1',
  eventId: 'e1',
  stage: 'candidate',
  priority: 'normal',
  notes: 'First notes',
  fields: { cost: '500 USD', sourceUrl: 'https://legacy.example.test/1', internal: 'keep' },
  revision: 3,
  createdAtUtc: '2026-11-01T10:00:00Z',
  updatedAtUtc: '2026-11-02T10:00:00Z',
  ...extra,
});
const conflict = () =>
  Response.json({ title: 'Conflict' }, { status: 409, headers: { 'Content-Type': 'application/problem+json' } });
const event = eventModel(
  'e1',
  {
    title: { 'ru-Cyrl': 'Выставка текстиля', en: 'Textile Exhibition' },
    starts: { value: '2026-12-05T19:00', precision: 'minute', certainty: 'announced' },
    timeZone: 'Asia/Tashkent',
    place: { city: 'Tashkent', country: 'UZ' },
  },
  {
    updatedAtUtc: '2026-11-05T10:00:00Z',
    sources: [
      {
        kind: 'scfd',
        id: 's/1',
        url: 'https://afisha.example.test/1',
        name: 'Afisha',
        state: 'suspect',
        firstSeenAtUtc: '2026-11-01T10:00:00Z',
        lastSeenAtUtc: '2026-11-04T10:00:00Z',
      },
      { kind: 'manual', id: 'person:1', firstSeenAtUtc: '2026-11-01T10:00:00Z', lastSeenAtUtc: '2026-11-01T10:00:00Z' },
    ],
  },
);

function router(put: () => Response = () => envelope(tracking({ revision: 4 }))) {
  return (request: Request) => {
    const path = new URL(request.url).pathname;
    if (path.endsWith('/workflow')) return envelope(workflow);
    if (path.endsWith('/history')) return envelope([]);
    if (request.method === 'PUT') return put();
    return envelope({ event, tracking: tracking() });
  };
}

describe('tracking form (PG15)', () => {
  async function render(responder: (request: Request) => Response, props: Record<string, unknown> = {}) {
    const service = fakeService(responder);
    const wrapper = mount(TrackingForm, {
      props: { client: service.client, catalog: 'ocharo', eventId: 'e1', ...props },
    });
    wrappers.push(wrapper);
    await flushPromises();
    return { ...service, wrapper };
  }

  it('keeps the draft on a conflict and offers the latest version', async () => {
    const { wrapper } = await render(router(conflict));

    await wrapper.get('textarea[name="notes"]').setValue('Edited notes');
    expect(wrapper.emitted('dirty')).toEqual([[true]]);
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    expect(wrapper.get('[data-test="tracking-conflict"]').text()).toContain('This record changed');
    expect((wrapper.get('textarea[name="notes"]').element as HTMLTextAreaElement).value).toBe('Edited notes');
    await wrapper.get('[data-test="tracking-conflict"] button').trigger('click');
    await flushPromises();
    expect((wrapper.get('textarea[name="notes"]').element as HTMLTextAreaElement).value).toBe('First notes');
    expect(wrapper.emitted('dirty')).toEqual([[true], [false]]);
  });

  it('shows a read-only field, leaves a hidden one out, and keeps both as stored', async () => {
    const { wrapper, requests } = await render(router(), { hiddenFields: ['internal'] });

    expect(wrapper.get('[data-test="tracking-read-only"]').text()).toContain('https://legacy.example.test/1');
    expect(wrapper.find('textarea[name="field-sourceUrl"]').exists()).toBe(false);
    expect(wrapper.find('textarea[name="field-internal"]').exists()).toBe(false);
    await wrapper.get('textarea[name="field-cost"]').setValue('600 USD');
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    const body = await requests.find((request) => request.method === 'PUT')!.json();
    expect(body.fields).toEqual({ cost: '600 USD', sourceUrl: 'https://legacy.example.test/1', internal: 'keep' });
    expect(body.expectedRevision).toBe(3);
  });
});

describe('event detail (PG04, PG07, PG08, PG11, PG13, PG17)', () => {
  it('lists each source with its state, the shown fields, the source update and the calendar action', async () => {
    const service = fakeService(router());
    const wrapper = mount(EventDetail, {
      props: {
        client: service.client,
        catalog: 'ocharo',
        eventId: 'e1',
        locale: 'ru',
        trackable: true,
        viewerZone: 'Asia/Tashkent',
        regions: [{ key: 'local', label: { en: 'Local', ru: 'Местные' }, countries: ['UZ'] }],
        shownFields: [{ key: 'cost', label: { ru: 'Стоимость' } }],
        hiddenFields: ['internal'],
      },
    });
    wrappers.push(wrapper);
    await flushPromises();

    expect(wrapper.get('#event-detail-title').text()).toBe('Выставка текстиля');
    expect(wrapper.get('[data-test="event-scope"]').text()).toBe('Местные');
    // The header and source pills take the fe sdk's default pill size (24 px, px-2.5), as the SCFD pills do.
    for (const pill of [
      wrapper.get('[data-test="event-scope"]'),
      wrapper.get('[data-test="event-source"] span.inline-flex'),
    ]) {
      expect(pill.classes()).toEqual(expect.arrayContaining(['h-6', 'px-2.5']));
      expect(pill.classes()).not.toContain('h-5');
    }
    expect(wrapper.get('[data-test="event-dates"]').text()).toBe('5 дек. 2026, 19:00');
    expect(wrapper.text()).not.toContain('Asia/Tashkent');
    const sources = wrapper.findAll('[data-test="event-source"]');
    expect(sources[0]!.get('a').text()).toBe('Afisha');
    expect(sources[0]!.get('a').attributes('href')).toBe('https://afisha.example.test/1');
    expect(sources[0]!.text()).toContain('Требует проверки');
    expect(sources[1]!.text()).toMatch(/^Источник без названия/);
    expect(wrapper.get('[data-test="event-source-updated"]').text()).toContain('Источник обновил');
    expect(wrapper.get('[data-test="event-field-cost"]').text()).toContain('500 USD');
    expect(wrapper.find('[data-test="event-calendar"]').exists()).toBe(true);
    expect(wrapper.find('textarea[name="field-internal"]').exists()).toBe(false);
    expect(service.requests.some((request) => new URL(request.url).pathname.endsWith('/events/e1'))).toBe(false);
  });
});

describe('event detail images (imageSrc)', () => {
  const first = { url: 'https://source.example.test/a.jpg', fileRef: 'f1', attribution: 'Credit' };
  const second = { url: 'https://source.example.test/b.jpg' };
  const imaged = eventModel('e2', { title: { en: 'Imaged' }, images: [first, second] });
  const copy = 'https://files.example.test/f1';

  async function renderDetail(props: Record<string, unknown>) {
    const service = fakeService(() => envelope(imaged));
    const wrapper = mount(EventDetail, {
      props: { client: service.client, catalog: 'ocharo', eventId: 'e2', ...props },
    });
    wrappers.push(wrapper);
    await flushPromises();
    return wrapper;
  }
  const sources = (wrapper: Awaited<ReturnType<typeof renderDetail>>) =>
    wrapper.findAll('[data-test="event-image"]').map((image) => image.attributes('src'));

  it('shows the resolver source, the url when it is null or fails, and drops an image whose sources all fail', async () => {
    const resolver = vi.fn((_event: EventModel, image: EventImage) =>
      image.fileRef ? `https://files.example.test/${image.fileRef}` : null,
    );
    const wrapper = await renderDetail({ imageSrc: resolver });

    // A hit uses its source; null keeps the url.
    expect(resolver).toHaveBeenCalledWith(imaged, first, 0);
    expect(resolver).toHaveBeenCalledWith(imaged, second, 1);
    expect(sources(wrapper)).toEqual([copy, second.url]);
    expect(wrapper.get('[data-test="event-image"]').attributes()).toMatchObject({
      loading: 'lazy',
      referrerpolicy: 'no-referrer',
      alt: 'Image: Credit',
    });

    // A failed source falls back to the url.
    await wrapper.findAll('[data-test="event-image"]')[0]!.trigger('error');
    expect(sources(wrapper)).toEqual([first.url, second.url]);

    // Both failing leaves the image out; the next one stays.
    await wrapper.findAll('[data-test="event-image"]')[0]!.trigger('error');
    expect(sources(wrapper)).toEqual([second.url]);

    // With no image left the strip goes, as with no images at all.
    await wrapper.get('[data-test="event-image"]').trigger('error');
    expect(wrapper.find('[data-test="event-images"]').exists()).toBe(false);
  });

  it('keeps each image at its url with no resolver', async () => {
    const wrapper = await renderDetail({});

    expect(sources(wrapper)).toEqual([first.url, second.url]);
  });
});

describe('deadlines panel (PG14)', () => {
  it('asks for the kinds named, reads "Apply by", and links each event', async () => {
    const service = fakeService(() =>
      envelope({
        byStage: {},
        untracked: 1,
        upcomingDeadlines: [
          {
            eventId: 'e1',
            title: { en: 'Fashion Week' },
            deadline: { kind: 'application', at: { value: '2026-11-20', precision: 'day', certainty: 'announced' } },
            stage: 'candidate',
            tracked: false,
          },
        ],
      }),
    );
    const wrapper = mount(EventsDeadlines, {
      props: {
        client: service.client,
        catalog: 'ocharo',
        today: '2026-11-10',
        stages: workflow.stages,
        deadlineKinds: ['application'],
        eventHref: (id: string) => `/events/${id}`,
      },
    });
    wrappers.push(wrapper);
    await flushPromises();

    expect(new URL(service.requests[0]!.url).searchParams.getAll('deadlineKinds')).toEqual(['application']);
    expect(wrapper.get('[data-test="deadline-when"]').text()).toBe('Apply by 20 Nov 2026');
    expect(wrapper.get('[data-test="deadline"] a').attributes('href')).toBe('/events/e1');
    expect(wrapper.get('[data-test="deadline"]').text()).toContain('Candidate');
    const stage = wrapper
      .get('[data-test="deadline"]')
      .findAll('span')
      .find((item) => item.text() === 'Candidate')!;
    expect(stage.classes()).toEqual(expect.arrayContaining(['h-6', 'px-2.5']));
    expect(stage.classes()).not.toContain('h-5');
    await wrapper.get('[data-test="deadline"] a').trigger('click', { button: 0 });
    expect(wrapper.emitted('open')).toEqual([['e1']]);
  });
});

describe('proposal form (PG04, PG16)', () => {
  it('adds the deadline and source, then writes the initial tracking', async () => {
    const created = eventModel('new', { title: { und: 'Pop-up' } }, { review: 'published' });
    const service = fakeService((request) =>
      request.method === 'POST'
        ? envelope(created, { status: 201 })
        : envelope(tracking({ eventId: 'new', revision: 1 })),
    );
    const wrapper = mount(EventProposalForm, {
      props: {
        client: service.client,
        catalog: 'ocharo',
        withDeadline: true,
        withFoundAt: true,
        privateCatalog: true,
        sourceField: 'sourceUrl',
        regions: [{ key: 'local', label: { en: 'Local' }, countries: ['UZ'] }],
        initialTracking: { stage: 'shortlisted', priority: 'high', fields: { cost: '100 USD' } },
      },
    });
    wrappers.push(wrapper);

    expect(wrapper.get('[data-test="facts-scope"]').text()).toBe('Scope unknown until a country is entered.');
    await wrapper.get('input[name="title"]').setValue('Pop-up');
    wrapper.getComponent(CountryPicker).vm.$emit('update:modelValue', 'UZ');
    await flushPromises();
    expect(wrapper.get<HTMLInputElement>('input[name="country"]').element.value).toBe('UZ');
    expect(wrapper.get('[data-test="facts-scope"]').text()).toBe('Local: Uzbekistan');
    await wrapper.get('input[name="applicationDeadline"]').setValue('2026-11-25');
    await wrapper.get('input[name="foundAt"]').setValue('https://t.example.test/post/1');
    expect(wrapper.get('button[type="submit"]').text()).toBe('Add event');
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    const [post, put] = service.requests;
    expect(await post!.json()).toMatchObject({
      facts: {
        place: { country: 'UZ' },
        website: 'https://t.example.test/post/1',
        deadlines: [{ kind: 'application', at: { value: '2026-11-25', precision: 'day', certainty: 'announced' } }],
      },
    });
    expect(new URL(put!.url).pathname).toBe('/api/events/v1/catalogs/ocharo/tracking/new');
    expect(await put!.json()).toEqual({
      stage: 'shortlisted',
      priority: 'high',
      fields: { cost: '100 USD', sourceUrl: 'https://t.example.test/post/1' },
      expectedRevision: null,
    });
    expect(wrapper.emitted('proposed')![0]![1]).toMatchObject({ eventId: 'new' });
  });
});

describe('event facts country (CountryPicker)', () => {
  const regions = [
    { key: 'local', label: { en: 'Local' }, countries: ['uz'] },
    { key: 'regional', label: { en: 'Regional' }, countries: ['KZ', 'RU', 'UZ'] },
    { key: 'international', label: { en: 'International' }, otherCountries: true },
  ];

  it('picks the country with the fe sdk picker and stores its alpha-2 code, with no two-letter hint', async () => {
    const wrapper = mount(EventFactsFields, {
      props: { modelValue: eventFactsDraft(undefined, 'en'), regions },
    });
    wrappers.push(wrapper);

    expect(wrapper.find('input[name="country"][maxlength]').exists()).toBe(false);
    expect(wrapper.text()).not.toContain('two-letter');
    const picker = wrapper.getComponent(CountryPicker);
    expect(picker.props('modelValue')).toBeNull();

    picker.vm.$emit('update:modelValue', 'UZ');
    await flushPromises();

    const emitted = wrapper.emitted('update:modelValue')!;
    expect(emitted.at(-1)![0]).toMatchObject({ country: 'UZ' });
    await wrapper.setProps({ modelValue: { ...eventFactsDraft(undefined, 'en'), country: 'UZ' } });
    expect(wrapper.getComponent(CountryPicker).props('modelValue')).toBe('UZ');

    wrapper.getComponent(CountryPicker).vm.$emit('update:modelValue', null);
    await flushPromises();
    expect(wrapper.emitted('update:modelValue')!.at(-1)![0]).toMatchObject({ country: '' });
  });

  it("lists the countries of the consumer's regions first, once each, and none without regions", () => {
    const withRegions = mount(EventFactsFields, { props: { modelValue: eventFactsDraft(undefined, 'en'), regions } });
    const without = mount(EventFactsFields, { props: { modelValue: eventFactsDraft(undefined, 'en') } });
    wrappers.push(withRegions, without);

    expect(withRegions.getComponent(CountryPicker).props('preferred')).toEqual(['UZ', 'KZ', 'RU']);
    expect(without.getComponent(CountryPicker).props('preferred')).toBeUndefined();
  });
});

/** The event page at the two widths of the dev review (390 px and 1440 px): rows 19 to 23 of the Events app review. */
describe('event detail on a phone and a desktop (events review #19-#23)', () => {
  const photo = { url: 'https://cdn.example.test/hall.jpg', fileRef: 'f-hall', attribution: 'Credit' };
  const poster = { url: 'https://cdn.example.test/poster.jpg' };
  const detail = eventModel('e5', {
    title: { en: 'Bigger than Life' },
    starts: { value: '2026-10-01', precision: 'day', certainty: 'announced' },
    ends: { value: '2026-12-01', precision: 'day', certainty: 'announced' },
    place: { venue: 'NBU Gallery', city: 'Tashkent', country: 'UZ' },
    attendance: 'offline',
    languages: ['ru-Cyrl'],
    // The same photo again: its address with a fragment, and its stored file under another address.
    images: [
      photo,
      poster,
      { url: 'https://cdn.example.test/hall.jpg#again' },
      { url: 'https://cdn.example.test/hall-2.jpg', fileRef: 'F-HALL' },
    ],
  });

  async function show(facts = detail, props: Record<string, unknown> = {}) {
    const service = fakeService(() => envelope(facts));
    const wrapper = mount(EventDetail, {
      props: { client: service.client, catalog: 'ocharo', eventId: facts.id, today: '2026-11-10', ...props },
    });
    wrappers.push(wrapper);
    await flushPromises();
    return wrapper;
  }

  /** The text under the Languages heading, null when the page has none. */
  const languagesOf = (wrapper: Awaited<ReturnType<typeof show>>) =>
    wrapper
      .findAll('dt')
      .find((heading) => heading.text() === 'Languages' || heading.text() === 'Языки')
      ?.element.nextElementSibling?.textContent?.trim() ?? null;

  it('#19, #20: Download calendar is a padding-free link-shaped button, level with the labels, 40 px high on a phone', async () => {
    const button = (await show()).get('[data-test="event-calendar"]').element;

    // `p-0` (the link variant's `p-0!`) leaves no left padding, so its text starts where WHEN and the dates above do.
    for (const width of [phone, desktop]) expect(utilitiesAt(button, width)).toContain('p-0');
    expect(heightAt(button, phone)).toBeGreaterThanOrEqual(40);
    expect(heightAt(button, desktop)).toBe(32);
  });

  it('#21: shows a repeated photo once, whatever its fragment or the address of its stored copy', async () => {
    const wrapper = await show();

    expect(wrapper.findAll('[data-test="event-image"]').map((image) => image.attributes('src'))).toEqual([
      photo.url,
      poster.url,
    ]);
    expect(distinctEventImages(detail.facts.images).map((item) => item.index)).toEqual([0, 1]);
  });

  it('#21: the source link is no identity, and two different photos both stay', () => {
    const shared = 'https://listing.example.test/event/1';

    expect(
      distinctEventImages([
        { url: 'https://cdn.example.test/a.jpg', sourceUrl: shared },
        { url: 'https://cdn.example.test/b.jpg', sourceUrl: shared },
        { url: 'javascript:alert(1)' },
        { url: 'javascript:alert(1)' },
      ]).map((item) => item.index),
    ).toEqual([0, 1, 2]);
    expect(distinctEventImages(undefined)).toEqual([]);
  });

  it('#22: names the languages in words, once each, and leaves the unknown one out', async () => {
    const english = await show();
    expect(languagesOf(english)).toBe('Russian');
    expect(english.text()).not.toContain('ru-Cyrl');

    const russian = await show(detail, { locale: 'ru' });
    expect(languagesOf(russian)).toBe('Русский');

    const several = await show(eventModel('e6', { languages: ['uz-Latn', 'ru', 'ru-Cyrl', 'und', 'uz-Cyrl'] }));
    expect(languagesOf(several)).toBe('Uzbek, Russian, Uzbek (Cyrillic)');

    const unknown = await show(eventModel('e7', { languages: ['und'] }));
    expect(languagesOf(unknown)).toBeNull();
  });

  it('#22: languageName reads a tag as a name, and keeps a tag the browser cannot name', () => {
    expect(languageName('ru-Cyrl', 'en')).toBe('Russian');
    expect(languageName('ru-Cyrl', 'ru')).toBe('Русский');
    expect(languageName('uz-Latn', 'en')).toBe('Uzbek');
    expect(languageName('uz-Cyrl', 'en')).toBe('Uzbek (Cyrillic)');
    expect(languageName('und', 'en')).toBeNull();
    expect(languageName('  ', 'en')).toBeNull();
    expect(languageName(null, 'en')).toBeNull();
    expect(languageName('not a tag!', 'en')).toBe('not a tag!');
  });

  it('#23: WHEN and WHERE share a column pair no wider than 768 px on a desktop, and stack on a phone', async () => {
    const grid = (await show()).get('dl').element;

    expect(columnsAt(grid, phone)).toBe(1);
    expect(columnsAt(grid, desktop)).toBe(2);
    // Two equal halves of a 1440 px page put the columns about 1150 px apart; the cap keeps them a reading distance apart.
    expect(maxWidthAt(grid, desktop)).toBe(768);
  });
});

/** The add-event form at the two widths of the dev review: rows 30 and 31 of the Events app review. */
describe('proposal form on a phone and a desktop (events review #30, #31)', () => {
  const kinds = [{ key: 'runway', label: { en: 'Runway show' } }];
  const regions = [{ key: 'local', label: { en: 'Local' }, countries: ['UZ'] }];

  function open(props: Record<string, unknown> = {}) {
    const wrapper = mount(EventProposalForm, {
      props: {
        client: fakeService(() => envelope({})).client,
        catalog: 'ocharo',
        kinds,
        regions,
        withDeadline: true,
        withFoundAt: true,
        privateCatalog: true,
        ...props,
      },
    });
    wrappers.push(wrapper);
    return wrapper;
  }

  it('#30: names every field in one label style, the date’s three parts included', () => {
    const wrapper = open();
    const names = [
      'Title',
      'Kind',
      'Starts',
      'Ends',
      'Precision',
      'Date',
      'Certainty',
      'Venue',
      'City',
      'Country',
      'Application deadline',
      'Organizer',
      'Website',
      'Where it was found',
    ];
    const labels = wrapper
      .findAll('legend, label > span, div > span')
      .filter((element) => names.includes(element.text()));

    expect(new Set(labels.map((label) => label.text()))).toEqual(new Set(names));
    for (const label of labels) {
      expect(utilitiesAt(label.element, desktop)).toEqual(expect.arrayContaining(['text-sm', 'font-medium']));
      expect(utilitiesAt(label.element, desktop)).not.toContain('text-xs');
      expect(utilitiesAt(label.element, desktop)).not.toContain('text-muted-foreground');
    }
  });

  it('#30: the date box carries a hint while the precision is "not announced", and the format once it is set', async () => {
    const wrapper = open();
    const starts = wrapper.get<HTMLInputElement>('input[name="starts-value"]');

    expect(starts.element.disabled).toBe(true);
    expect(starts.attributes('placeholder')).toBe('Pick a precision first');
    expect(wrapper.get('input[name="ends-value"]').attributes('placeholder')).toBe('Pick a precision first');

    await wrapper.get('select[name="starts-precision"]').setValue('day');
    expect(wrapper.get<HTMLInputElement>('input[name="starts-value"]').element.disabled).toBe(false);
    expect(wrapper.get('input[name="starts-value"]').attributes('placeholder')).toBe('2026-03-14');
  });

  it('#30: Kind takes the full row like the title, and shares it only beside a status', () => {
    const alone = open();
    const beside = mount(EventFactsFields, {
      props: { modelValue: eventFactsDraft(undefined, 'en'), kinds, withStatus: true },
    });
    wrappers.push(beside);

    const rowOf = (wrapper: ReturnType<typeof mount>) =>
      wrapper.get('select[name="kind"]').element.closest('div.grid')!;
    expect(columnsAt(rowOf(alone), desktop)).toBe(1);
    expect(columnsAt(rowOf(alone), phone)).toBe(1);
    expect(columnsAt(rowOf(beside), desktop)).toBe(2);
    expect(columnsAt(rowOf(beside), phone)).toBe(1);
  });

  it('#31: every field is at least 40 px high on a phone, native ones and the fe sdk’s alike', () => {
    const wrapper = open();
    const fields = wrapper.findAll('input, select').filter((field) => field.attributes('type') !== 'hidden');

    expect(fields.map((field) => field.attributes('name'))).toEqual(
      expect.arrayContaining([
        'title',
        'kind',
        'starts-precision',
        'starts-value',
        'starts-certainty',
        'ends-value',
        'venue',
        'city',
        'applicationDeadline',
        'organizer',
        'website',
        'foundAt',
      ]),
    );
    for (const field of fields) {
      expect(heightAt(field.element, phone), field.attributes('name')).toBeGreaterThanOrEqual(40);
    }
  });
});
