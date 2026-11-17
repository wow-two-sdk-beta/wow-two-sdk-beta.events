import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DigestsPanel, TelegramText } from '../src/admin';
import type { DigestModel, DigestRunModel } from '../src/contracts';
import { envelope, fakeService } from './Fixtures';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
  vi.unstubAllGlobals();
});

const linked = '0193f3a4-0000-7000-8000-00000000000a';
const unlinked = '0193f3a4-0000-7000-8000-00000000000b';
const digest: DigestModel = {
  id: 'd1',
  catalog: 'ocharo-fashion',
  name: 'Ocharo weekly',
  enabled: true,
  channel: 'telegram',
  schedule: { days: ['monday'], times: ['09:00'], timeZone: 'Asia/Tashkent' },
  selection: { newSinceLastRun: true, startingWithinDays: 30, deadlineWithinDays: 14 },
  recipients: [{ subject: linked }, { subject: unlinked, language: 'ru' }],
  language: 'uz-Latn',
  itemCap: 20,
  nextRunAtUtc: '2026-11-23T04:00:00Z',
  revision: 1,
  createdAtUtc: '2026-11-17T08:00:00Z',
  updatedAtUtc: '2026-11-17T08:00:00Z',
};
const run: DigestRunModel = {
  id: 'r1',
  digestId: 'd1',
  occurrenceAtUtc: '2026-11-17T09:00:00Z',
  trigger: 'manual',
  status: 'completed',
  matched: 3,
  selected: 3,
  texts: [{ language: 'uz-Latn', text: '<b>Ocharo weekly</b>', recipients: 1 }],
  recipients: [
    { subject: linked, language: 'uz-Latn', status: 'sent', attempts: 1, updatedAtUtc: '2026-11-17T09:00:01Z' },
    { subject: unlinked, language: 'ru', status: 'not_linked', attempts: 1, updatedAtUtc: '2026-11-17T09:00:01Z' },
  ],
  startedAtUtc: '2026-11-17T09:00:00Z',
  finishedAtUtc: '2026-11-17T09:00:01Z',
};

/** Answers the panel's routes by path; records every request. */
function serve(digests: DigestModel[] = [digest]) {
  return fakeService(async (request) => {
    const path = new URL(request.url).pathname;
    if (request.method === 'GET' && path.endsWith('/digests')) return envelope(digests);
    if (request.method === 'POST' && path.endsWith('/digests'))
      return envelope({ ...digest, ...(await request.clone().json()) }, { status: 201 });
    if (request.method === 'GET' && path.endsWith('/digests/d1')) return envelope(digest);
    if (request.method === 'GET' && path.endsWith('/runs')) return envelope({ items: [run], nextCursor: null });
    if (path.endsWith('/preview'))
      return envelope({
        digestId: 'd1',
        asOfUtc: '2026-11-17T09:00:00Z',
        matched: 1,
        items: [{ eventId: 'e1', title: { en: 'Fair' }, starts: { value: '2026-12-01', precision: 'day' } }],
        texts: [
          {
            language: 'uz-Latn',
            text: '<b>Ocharo &amp; co</b>\n<a href="https://app.test/e1">Fair</a>',
            recipients: 2,
          },
        ],
        recipients: run.recipients,
      });
    if (path.endsWith('/send')) return envelope(run);
    return Response.json({ title: 'Not found' }, { status: 404 });
  });
}

async function render(service = serve(), props: Record<string, unknown> = {}) {
  const wrapper = mount(DigestsPanel, { props: { client: service.client, catalog: 'ocharo-fashion', ...props } });
  wrappers.push(wrapper);
  await flushPromises();
  await flushPromises();
  return { ...service, wrapper };
}

describe('digests panel', () => {
  it('lists the digests with the next run in the schedule’s own zone, and opens the first', async () => {
    const { wrapper } = await render();

    expect(wrapper.get('[data-test="digest-item"]').text()).toContain('Ocharo weekly');
    expect(wrapper.get('[data-test="digest-next-run"]').text()).toContain('Mon, Nov 23, 2026, 09:00 (Asia/Tashkent)');
    expect(wrapper.get('[data-test="digest-item"]').text()).toContain('Mon, Nov 23, 2026, 09:00 (Asia/Tashkent)');
    expect((wrapper.get('input[name="times"]').element as HTMLInputElement).value).toBe('09:00');
    expect((wrapper.get('textarea[name="recipients"]').element as HTMLTextAreaElement).value).toBe(
      `${linked}\n${unlinked} ru`,
    );
  });

  it('shows each run’s recipients in Identity’s words', async () => {
    const { wrapper } = await render();

    const runRow = wrapper.get('[data-test="digest-run"]');
    expect(runRow.get('[data-test="digest-run-counts"]').text()).toContain('not_linked 1');
    await runRow.get('button').trigger('click');
    const statuses = wrapper
      .findAll('[data-test="digest-runs"] [data-test="digest-recipient"]')
      .map((item) => item.attributes('data-status'));
    expect(statuses).toEqual(['sent', 'not_linked']);
  });

  it('creates a digest from the product’s defaults, with its fixed link', async () => {
    const service = serve([]);
    const { wrapper, requests } = await render(service, {
      defaults: { name: 'Telegram delivery', recipients: [{ role: 'member' }] },
      linkTemplate: 'https://events.ocharo.test/events/{id}',
      locale: 'uz',
    });

    expect(wrapper.find('input[name="linkTemplate"]').exists()).toBe(false);
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    const create = requests.find((request) => request.method === 'POST')!;
    expect(new URL(create.url).pathname).toBe('/api/events/v1/catalogs/ocharo-fashion/digests');
    expect(await create.json()).toMatchObject({
      name: 'Telegram delivery',
      channel: 'telegram',
      schedule: { days: ['monday'], times: ['09:00'], timeZone: 'Asia/Tashkent' },
      selection: { newSinceLastRun: true, startingWithinDays: 30, deadlineWithinDays: 14 },
      recipients: [{ role: 'member' }],
      language: 'uz-Latn',
      itemCap: 20,
      linkTemplate: 'https://events.ocharo.test/events/{id}',
      expectedRevision: null,
    });
    expect(create.headers.get('X-Csrf-Token')).toBe('csrf-1');
  });

  it('refuses a malformed time before calling the service', async () => {
    const { wrapper, requests } = await render(serve([]));
    await wrapper.get('input[name="times"]').setValue('9am');
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    expect(wrapper.get('[data-test="digest-problem"]').text()).toContain('HH:mm');
    expect(requests.some((request) => request.method === 'POST')).toBe(false);
  });

  it('previews the messages, then sends now after a confirmation in the app’s own dialog', async () => {
    const confirm = vi.fn(() => true);
    vi.stubGlobal('confirm', confirm);
    const { wrapper, requests } = await render();

    await wrapper.get('[data-test="digest-preview"]').trigger('click');
    await flushPromises();
    const preview = wrapper.get('[data-test="digest-preview-result"] [data-test="telegram-text"]');
    expect(preview.find('strong').text()).toBe('Ocharo & co');
    expect(preview.get('a').attributes('href')).toBe('https://app.test/e1');

    await wrapper.get('[data-test="digest-send"]').trigger('click');
    await flushPromises();
    await dialogIs(true);
    expect(document.body.querySelector('[role="alertdialog"]')?.textContent).toContain(
      'Send this digest to 2 recipients now?',
    );
    expect(sends(requests)).toBe(0);

    await dialogButton('digest-send-accept').click();
    await flushPromises();
    expect(confirm).not.toHaveBeenCalled();
    expect(sends(requests)).toBe(1);
    await dialogIs(false);
    expect(
      wrapper
        .findAll('[data-test="digest-sent"] [data-test="digest-recipient"]')
        .map((item) => item.attributes('data-status')),
    ).toEqual(['sent', 'not_linked']);
  });

  it('sends nothing when the dialog is cancelled or dismissed with Escape', async () => {
    const confirm = vi.fn(() => true);
    vi.stubGlobal('confirm', confirm);
    const { wrapper, requests } = await render();

    await wrapper.get('[data-test="digest-send"]').trigger('click');
    await flushPromises();
    await dialogIs(true);
    await dialogButton('digest-send-cancel').click();
    await dialogIs(false);

    await wrapper.get('[data-test="digest-send"]').trigger('click');
    await flushPromises();
    await dialogIs(true);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await dialogIs(false);

    expect(sends(requests)).toBe(0);
    expect(confirm).not.toHaveBeenCalled();
    expect(wrapper.find('[data-test="digest-sent"]').exists()).toBe(false);
  });

  it('asks in the reader’s language', async () => {
    const { wrapper } = await render(serve(), { locale: 'uz' });

    await wrapper.get('[data-test="digest-send"]').trigger('click');
    await flushPromises();

    expect(document.body.querySelector('[role="alertdialog"]')?.textContent).toContain(
      'Dayjest hozir 2 ta qabul qiluvchiga yuborilsinmi?',
    );
  });
});

/** Counts the send requests a panel made. */
const sends = (requests: readonly Request[]) =>
  requests.filter((request) => request.method === 'POST' && request.url.endsWith('/digests/d1/send')).length;

/** Waits for the confirmation dialog to open, or to leave the document after its closing animation. */
const dialogIs = (open: boolean) =>
  vi.waitFor(() => {
    const dialog = document.body.querySelector('[role="alertdialog"]');
    if (open) expect(dialog?.closest('[data-state]')?.getAttribute('data-state')).toBe('open');
    else expect(dialog).toBeNull();
  });

/** Finds a button of the open dialog, which sits in the document, outside the panel. */
function dialogButton(name: string): HTMLButtonElement {
  const button = document.body.querySelector<HTMLButtonElement>(`[data-test="${name}"]`);
  if (!button) throw new Error(`No dialog button ${name}`);
  return button;
}

describe('telegram text', () => {
  it('renders the subset and never a script or a foreign link', () => {
    const wrapper = mount(TelegramText, {
      props: {
        text: '<b>Bold</b> <i>it</i> <a href="javascript:alert(1)">bad</a> <script>alert(1)</script>&lt;tag&gt;',
      },
    });
    wrappers.push(wrapper);

    expect(wrapper.find('strong').text()).toBe('Bold');
    expect(wrapper.find('em').text()).toBe('it');
    expect(wrapper.find('a').exists()).toBe(false);
    expect(wrapper.find('script').exists()).toBe(false);
    expect(wrapper.text()).toContain('<tag>');
  });
});
