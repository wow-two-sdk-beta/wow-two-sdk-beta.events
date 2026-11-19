import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import type { TrackingModel, WorkflowModel } from '../src/contracts';
import { EventDetail, EventProposalForm, EventsDeadlines, TrackingForm } from '../src/kit';
import { envelope, eventModel, fakeService } from './Fixtures';

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
    await wrapper.get('input[name="country"]').setValue('uz');
    expect(wrapper.get('[data-test="facts-scope"]').text()).toBe('Local: Uzbekistan');
    await wrapper.get('input[name="applicationDeadline"]').setValue('2026-11-25');
    await wrapper.get('input[name="foundAt"]').setValue('https://t.example.test/post/1');
    expect(wrapper.get('button[type="submit"]').text()).toBe('Add event');
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    const [post, put] = service.requests;
    expect(await post!.json()).toMatchObject({
      facts: {
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
