import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import type { TrackedEventModel, TrackingModel, WorkflowModel } from '../src/contracts';
import { EventCard, EventsFeed, type EventRegion } from '../src/kit';
import { envelope, eventModel, fakeService } from './Fixtures';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
});

const workflow: WorkflowModel = {
  stages: [
    { key: 'candidate', label: { en: 'Candidate' }, class: 'open' },
    { key: 'shortlisted', label: { en: 'Shortlisted' }, class: 'active' },
    { key: 'skipped', label: { en: 'Skipped' }, class: 'lost' },
    { key: 'rejected', label: { en: 'Rejected' }, class: 'lost' },
  ],
  defaultStage: 'candidate',
  revision: 1,
};
const regions: EventRegion[] = [
  { key: 'local', label: { en: 'Local' }, countries: ['UZ'] },
  { key: 'regional', label: { en: 'Regional' }, countries: ['KZ', 'RU'] },
  { key: 'international', label: { en: 'International' }, otherCountries: true },
];
const tracking = (eventId: string, stage: string, extra: Partial<TrackingModel> = {}): TrackingModel => ({
  id: `t-${eventId}`,
  eventId,
  stage,
  priority: 'normal',
  revision: 2,
  createdAtUtc: '2026-11-01T10:00:00Z',
  updatedAtUtc: '2026-11-02T10:00:00Z',
  ...extra,
});
const imported = eventModel('imported', {
  title: { 'ru-Cyrl': 'Выставка текстиля' },
  starts: { value: '2026-12-12', precision: 'day', certainty: 'announced' },
  place: { city: 'Tashkent', country: 'UZ' },
});
const shortlisted = eventModel('shortlisted', {
  title: { en: 'Fashion Week' },
  starts: { value: '2027-03-14', precision: 'day', certainty: 'announced' },
  place: { city: 'Almaty', country: 'KZ' },
});
const page = (items: TrackedEventModel[]) => envelope({ items, nextCursor: null });
const summary = envelope({
  byStage: { candidate: 1, shortlisted: 1, skipped: 2, rejected: 0 },
  untracked: 1,
  upcomingDeadlines: [],
});

/** Answers each route of the tracking feed; `put` answers the quick action's write. */
function router(items: TrackedEventModel[], put: () => Response = () => envelope(tracking('imported', 'shortlisted'))) {
  return (request: Request) => {
    const path = new URL(request.url).pathname;
    if (path.endsWith('/workflow')) return envelope(workflow);
    if (path.endsWith('/tracking/summary')) return summary;
    if (request.method === 'PUT') return put();
    return page(items);
  };
}

async function render(props: Record<string, unknown>, responder: (request: Request) => Response) {
  const service = fakeService(responder);
  const wrapper = mount(EventsFeed, {
    props: { client: service.client, catalog: 'ocharo', today: '2026-11-10', tracking: true, ...props },
  });
  wrappers.push(wrapper);
  await flushPromises();
  return { ...service, wrapper };
}

const listRequests = (requests: Request[]) =>
  requests.filter((request) => request.method === 'GET' && new URL(request.url).pathname.endsWith('/tracking'));

describe('tracking feed (PG01, PG02, PG03, PG05, PG06, PG11)', () => {
  const items = [
    { event: imported, tracking: null },
    {
      event: shortlisted,
      tracking: tracking('shortlisted', 'shortlisted', { priority: 'high', fields: { cost: '500 USD' } }),
    },
  ];
  const ocharo = {
    hiddenStages: ['skipped', 'rejected'],
    regions,
    quickStages: ['shortlisted', 'skipped'],
    canTrack: true,
    shownFields: [{ key: 'cost', label: { en: "Ocharo's cost" }, empty: { en: 'Cost not stated' } }],
  };

  it("lists the organization's tracking, hiding skipped and rejected, with stage counts", async () => {
    const { wrapper, requests } = await render(ocharo, router(items));

    const url = new URL(listRequests(requests)[0]!.url);
    expect(url.searchParams.getAll('excludeStages')).toEqual(['skipped', 'rejected']);
    expect(url.searchParams.get('from')).toBe('2026-11-10');
    expect(wrapper.findAll('select[name="stage"] option').map((option) => option.text())).toEqual([
      'All stages (5)',
      'Candidate (2)',
      'Shortlisted (1)',
      'Skipped (2)',
      'Rejected (0)',
    ]);
    const cards = wrapper.findAll('[data-test="event-card"]');
    expect(cards[0]!.get('[data-test="event-untracked"]').text()).toBe('New — review');
    expect(cards[0]!.get('[data-test="event-stage"]').text()).toBe('Candidate');
    expect(cards[0]!.get('[data-test="title-language"]').text()).toBe('RU');
    expect(cards[0]!.get('[data-test="event-scope"]').text()).toBe('Local');
    expect(cards[0]!.get('[data-test="event-field-cost"]').text()).toBe('Cost not stated');
    expect(cards[1]!.get('[data-test="event-stage"]').text()).toBe('Shortlisted');
    expect(cards[1]!.text()).toContain('High priority');
    expect(cards[1]!.get('[data-test="event-field-cost"]').text()).toBe("Ocharo's cost: 500 USD");
    expect(cards[1]!.find('[data-test="event-quick-stages"]').exists()).toBe(false);
  });

  it('shows hidden stages on request or by their own filter, and filters by region chips', async () => {
    const { wrapper, requests } = await render(ocharo, router(items));

    await wrapper.get('input[name="showHidden"]').setValue(true);
    await flushPromises();
    expect(new URL(listRequests(requests).at(-1)!.url).searchParams.getAll('excludeStages')).toEqual([]);

    await wrapper.get('select[name="stage"]').setValue('skipped');
    await flushPromises();
    expect(new URL(listRequests(requests).at(-1)!.url).searchParams.get('stage')).toBe('skipped');
    expect(wrapper.find('input[name="showHidden"]').exists()).toBe(false);

    await wrapper.get('[data-region="regional"]').trigger('click');
    await flushPromises();
    expect(new URL(listRequests(requests).at(-1)!.url).searchParams.getAll('countries')).toEqual(['KZ', 'RU']);
    await wrapper.get('[data-region="international"]').trigger('click');
    await flushPromises();
    const international = new URL(listRequests(requests).at(-1)!.url).searchParams;
    expect(international.getAll('excludeCountries')).toEqual(['UZ', 'KZ', 'RU']);
    expect(international.get('includeUnknownCountry')).toBeNull();
    expect(wrapper.find('input[name="country"]').exists()).toBe(false);
  });

  it('moves an untracked event with a quick action, then reloads the list and the counts', async () => {
    const { wrapper, requests } = await render(ocharo, router(items));

    const buttons = wrapper.findAll('[data-test="event-quick-stages"] button');
    expect(buttons.map((button) => button.text())).toEqual(['Shortlisted', 'Skipped']);
    await buttons[0]!.trigger('click');
    await flushPromises();

    const put = requests.find((request) => request.method === 'PUT')!;
    expect(new URL(put.url).pathname).toBe('/api/events/v1/catalogs/ocharo/tracking/imported');
    expect(await put.json()).toEqual({ stage: 'shortlisted', expectedRevision: null });
    expect(listRequests(requests)).toHaveLength(2);
    expect(wrapper.emitted('tracked')).toHaveLength(1);
  });

  it('reloads and says so when the quick action meets a newer version', async () => {
    const conflict = () =>
      Response.json({ title: 'Conflict' }, { status: 409, headers: { 'Content-Type': 'application/problem+json' } });
    const { wrapper, requests } = await render(ocharo, router(items, conflict));

    await wrapper.findAll('[data-test="event-quick-stages"] button')[1]!.trigger('click');
    await flushPromises();

    expect(wrapper.get('[data-test="track-changed"]').text()).toContain('changed meanwhile');
    expect(listRequests(requests)).toHaveLength(2);
    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
  });
});

describe('event card (PG07, PG08, PG09, PG10)', () => {
  it('names its source, flags a removed listing, strikes a cancelled title and shows tickets and online', () => {
    const event = eventModel(
      'card',
      {
        title: { en: 'Pop-up' },
        status: 'cancelled',
        attendance: 'online',
        offers: [{ kind: 'ticket', minPrice: 150000, currency: 'UZS' }],
      },
      {
        sources: [
          {
            kind: 'scfd',
            id: 's/1',
            url: 'https://afisha.example.test/1',
            name: 'Afisha',
            state: 'gone',
            firstSeenAtUtc: '2026-11-01T10:00:00Z',
            lastSeenAtUtc: '2026-11-02T10:00:00Z',
          },
        ],
      },
    );
    const wrapper = mount(EventCard, { props: { event, today: '2026-11-10' } });
    wrappers.push(wrapper);

    expect(wrapper.get('[data-test="event-source"]').text()).toBe('via Afisha');
    expect(wrapper.get('[data-test="event-source"] a').attributes('href')).toBe('https://afisha.example.test/1');
    expect(wrapper.get('[data-test="event-removed"]').text()).toBe('Listing removed at source');
    expect(wrapper.get('h3 button').classes()).toContain('line-through');
    expect(wrapper.get('[data-test="event-online"]').text()).toBe('Online');
    expect(wrapper.get('[data-test="event-tickets"]').text()).toBe('Tickets: from 150 000 UZS');
    expect(wrapper.find('[data-test="title-language"]').exists()).toBe(false);
  });

  it('puts the source after its name in Uzbek', () => {
    const event = eventModel(
      'uz',
      {},
      {
        sources: [
          {
            kind: 'scfd',
            id: 's/2',
            name: 'Afisha',
            state: 'live',
            firstSeenAtUtc: '2026-11-01T10:00:00Z',
            lastSeenAtUtc: '2026-11-01T10:00:00Z',
          },
        ],
      },
    );
    const wrapper = mount(EventCard, { props: { event, locale: 'uz' } });
    wrappers.push(wrapper);

    expect(wrapper.get('[data-test="event-source"]').text()).toBe('Afisha orqali');
    expect(wrapper.find('[data-test="event-removed"]').exists()).toBe(false);
  });
});
