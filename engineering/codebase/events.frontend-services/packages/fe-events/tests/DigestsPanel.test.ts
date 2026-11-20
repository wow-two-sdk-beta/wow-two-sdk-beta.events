import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DigestsPanel, TelegramText } from '../src/admin';
import type { DigestModel, DigestRunModel, WorkflowModel } from '../src/contracts';
import { envelope, fakeService } from './Fixtures';
import { desktop, heightAt, phone, utilitiesAt } from './Viewport';

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

/** Ocharo's workflow: an untracked event reads as `candidate`. */
const workflow: WorkflowModel = {
  stages: [
    { key: 'candidate', label: { en: 'New', uz: 'Yangi' }, class: 'open' },
    { key: 'shortlisted', label: { en: 'Shortlisted', uz: 'Saralangan' }, class: 'active' },
    { key: 'applying', label: { en: 'Applying', uz: 'Ariza topshirilmoqda' }, class: 'active' },
    { key: 'skipped', label: { en: 'Skipped', uz: 'O‘tkazib yuborilgan' }, class: 'lost' },
  ],
  defaultStage: 'candidate',
  revision: 1,
};

/** Answers the panel's routes by path; records every request. */
function serve(digests: DigestModel[] = [digest], options: { workflow?: boolean } = {}) {
  return fakeService(async (request) => {
    const path = new URL(request.url).pathname;
    if (request.method === 'GET' && path.endsWith('/workflow'))
      return options.workflow === false ? Response.json({ title: 'Not found' }, { status: 404 }) : envelope(workflow);
    if (request.method === 'GET' && path.endsWith('/digests')) return envelope(digests);
    if (request.method === 'POST' && path.endsWith('/digests'))
      return envelope({ ...digest, ...(await request.clone().json()) }, { status: 201 });
    if (request.method === 'PUT' && path.endsWith('/digests/d1'))
      return envelope({ ...digest, ...(await request.clone().json()), revision: 2 });
    if (request.method === 'GET' && path.endsWith('/digests/d1')) return envelope(digests[0] ?? digest);
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
  it('opens the first digest, with its next run in the schedule’s own zone', async () => {
    const { wrapper } = await render();

    expect(wrapper.get('[data-test="digest-editor"] h2').text()).toBe('Ocharo weekly');
    expect(wrapper.get('[data-test="digest-next-run"]').text()).toContain('Mon, Nov 23, 2026, 09:00 (Asia/Tashkent)');
    expect((wrapper.get('input[name="times"]').element as HTMLInputElement).value).toBe('09:00');
    expect((wrapper.get('textarea[name="recipients"]').element as HTMLTextAreaElement).value).toBe(
      `${linked}\n${unlinked} ru`,
    );
  });

  it('shows each run’s recipients in Identity’s words', async () => {
    const { wrapper } = await render();

    const runRow = wrapper.get('[data-test="digest-run"]');
    expect(runRow.get('[data-test="digest-run-counts"]').text()).toBe(
      'Delivered 1 · No Telegram link 1 · Muted 0 · Failed 0',
    );
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

  it('picks stages from the organization’s workflow, never one stage in both lists', async () => {
    const { wrapper, requests } = await render();
    const stages = wrapper.get('[data-test="digest-stages"]');

    expect(stages.text()).toContain('Shortlisted');
    expect(wrapper.get('[data-test="digest-stages-hint"]').text()).toContain('“New”');
    await wrapper.get('input[name="exclude-stage-skipped"]').setValue(true);
    await wrapper.get('input[name="stage-shortlisted"]').setValue(true);
    await wrapper.get('input[name="stage-applying"]').setValue(true);
    await wrapper.get('input[name="exclude-stage-applying"]').setValue(true);
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    expect((wrapper.get('input[name="stage-applying"]').element as HTMLInputElement).checked).toBe(false);
    const update = requests.find((request) => request.method === 'PUT')!;
    expect(new URL(update.url).pathname).toBe('/api/events/v1/catalogs/ocharo-fashion/digests/d1');
    expect((await update.json()).selection).toMatchObject({
      stages: ['shortlisted'],
      excludeStages: ['skipped', 'applying'],
    });
  });

  it('keeps the digest’s stages, without a notice, when the workflow cannot be read', async () => {
    const staged: DigestModel = { ...digest, selection: { ...digest.selection, excludeStages: ['skipped'] } };
    const { wrapper, requests } = await render(serve([staged], { workflow: false }));

    expect(wrapper.find('[role="alert"]').exists()).toBe(false);
    expect(wrapper.find('[data-test="digest-stages-hint"]').exists()).toBe(false);
    expect((wrapper.get('input[name="exclude-stage-skipped"]').element as HTMLInputElement).checked).toBe(true);
    await wrapper.get('form').trigger('submit');
    await flushPromises();

    const update = requests.find((request) => request.method === 'PUT')!;
    expect((await update.json()).selection).toMatchObject({ stages: [], excludeStages: ['skipped'] });
  });

  it('takes the product’s workflow without reading one', async () => {
    const { wrapper, requests } = await render(serve(), { workflow, locale: 'uz' });

    expect(requests.some((request) => new URL(request.url).pathname.endsWith('/workflow'))).toBe(false);
    expect(wrapper.get('[data-test="digest-exclude-stages"]').text()).toContain('O‘tkazib yuborilgan');
    expect(wrapper.get('[data-test="digest-stages-hint"]').text()).toContain('“Yangi”');
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

/** The Telegram delivery panel at the two widths of the dev review: rows 26 to 29 of the Events app review. */
describe('digests panel on a phone and a desktop (events review #26-#29)', () => {
  const second: DigestModel = { ...digest, id: 'd2', name: 'Ocharo monthly', nextRunAtUtc: '2026-12-01T04:00:00Z' };

  it('#28: shows a lone digest once: the editor carries its name and next run, with no list row repeating them', async () => {
    const { wrapper } = await render();

    expect(wrapper.find('[data-test="digest-list"]').exists()).toBe(false);
    expect(wrapper.text().split('Ocharo weekly').length - 1).toBe(1);
    expect(wrapper.text().split('Mon, Nov 23, 2026, 09:00').length - 1).toBe(1);
  });

  it('#28: lists the digests from the second one on, and does not repeat the open one’s next run in its row', async () => {
    const { wrapper } = await render(serve([digest, second]));

    const rows = wrapper.findAll('[data-test="digest-item"]');
    expect(rows.map((row) => row.text())).toEqual([
      'Ocharo weekly',
      expect.stringContaining('Next run: Tue, Dec 1, 2026, 09:00 (Asia/Tashkent)'),
    ]);
    expect(wrapper.get('[data-test="digest-next-run"]').text()).toContain('Mon, Nov 23, 2026, 09:00');
    expect(wrapper.text().split('Mon, Nov 23, 2026, 09:00').length - 1).toBe(1);
  });

  it('#28: keeps the list while a new digest is written, to go back to the first', async () => {
    const { wrapper } = await render();

    await wrapper.get('[data-test="digest-new"]').trigger('click');
    expect(wrapper.findAll('[data-test="digest-item"]')).toHaveLength(1);
  });

  it('#28: says every recipient status in words, in the counts and in the recipient list, never as a raw code', async () => {
    const { wrapper } = await render();

    const run = wrapper.get('[data-test="digest-run"]');
    await run.get('button').trigger('click');
    const history = wrapper.get('[data-test="digest-runs"]');
    const recipients = history.findAll('[data-test="digest-recipient"]');

    expect(recipients.map((item) => item.find('span.inline-flex').text())).toEqual(['Delivered', 'No Telegram link']);
    expect(history.text()).not.toMatch(/not_linked|\bsent\b|\bmuted\b/);
    expect(history.find('code').exists()).toBe(false);
    // The raw word stays a data attribute, for tests and tooling, and is never shown.
    expect(recipients.map((item) => item.attributes('data-status'))).toEqual(['sent', 'not_linked']);
  });

  it('#26: every checkbox is 20 px and sits in a row that is a 40 px tap target', async () => {
    const { wrapper } = await render(serve(), { workflow });

    const boxes = wrapper.findAll('input[type="checkbox"]');
    expect(boxes.length).toBeGreaterThanOrEqual(20);
    for (const box of boxes) {
      expect(utilitiesAt(box.element, phone), box.attributes('name')).toContain('size-5');
      const row = box.element.closest('label')!;
      expect(heightAt(row, phone), box.attributes('name')).toBeGreaterThanOrEqual(40);
    }
  });

  it('#26: Refresh, New digest and every text field are at least 40 px high on a phone', async () => {
    const { wrapper } = await render();

    const buttons = wrapper.findAll(
      '[data-test="digests-panel"] > header button, [data-test="digest-runs"] header button',
    );
    expect(buttons.map((button) => button.text())).toEqual(['Refresh', 'New digest', 'Refresh']);
    for (const button of buttons) expect(heightAt(button.element, phone)).toBeGreaterThanOrEqual(40);
    const fields = wrapper.findAll(
      '[data-test="digest-editor"] input:not([type="checkbox"]), [data-test="digest-editor"] select, [data-test="digest-editor"] textarea',
    );
    expect(fields.length).toBeGreaterThan(8);
    for (const field of fields)
      expect(heightAt(field.element, phone), field.attributes('name')).toBeGreaterThanOrEqual(40);
    expect(heightAt(wrapper.get('[data-test="digest-run"] button').element, phone)).toBeGreaterThanOrEqual(40);
  });

  it('#27: on a phone Save and Delete take the whole width of the actions, on a desktop Delete sits apart at the end', async () => {
    const { wrapper } = await render();

    const actions = wrapper.get('[data-test="digest-save"]').element.parentElement!;
    expect(utilitiesAt(actions, phone)).toEqual(expect.arrayContaining(['grid', 'grid-cols-2']));
    expect(utilitiesAt(actions, desktop)).toEqual(expect.arrayContaining(['flex', 'flex-wrap']));
    const remove = wrapper.get('[data-test="digest-delete"]').element;
    const save = wrapper.get('[data-test="digest-save"]').element;
    for (const button of [remove, save]) {
      expect(utilitiesAt(button, phone)).toContain('col-span-2');
      expect(utilitiesAt(button, desktop)).not.toContain('col-span-2');
    }
    expect(utilitiesAt(remove, desktop)).toContain('ms-auto');
    expect(utilitiesAt(remove, phone)).not.toContain('ms-auto');
    expect(utilitiesAt(wrapper.get('[data-test="digest-preview"]').element, phone)).not.toContain('col-span-2');
  });

  it('#29: the "Runs on its schedule" and "New since the last digest" rows sit level with the inputs beside them', async () => {
    const { wrapper } = await render();

    const rowOf = (name: string) => wrapper.get(`input[name="${name}"]`).element.closest('fieldset, div.grid')!;
    for (const name of ['enabled', 'newSinceLastRun']) {
      expect(utilitiesAt(rowOf(name), desktop), name).toContain('items-end');
      expect(heightAt(wrapper.get(`input[name="${name}"]`).element.closest('label')!, desktop), name).toBe(40);
    }
    // One column on a phone, where nothing sits beside anything.
    expect(utilitiesAt(rowOf('enabled'), phone)).not.toContain('items-end');
  });
});
