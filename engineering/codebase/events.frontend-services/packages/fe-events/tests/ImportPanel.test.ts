import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it } from 'vitest';
import { ImportPanel } from '../src/admin';
import { envelope, fakeService } from './Fixtures';

const wrappers: ReturnType<typeof mount>[] = [];
afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
});

const bundle = { format: 'wow2.events-import.v1', source: 'afisha-v1', rows: [{ legacyId: 'a' }, { legacyId: 'b' }] };
const report = {
  dryRun: true,
  source: 'afisha-v1',
  created: 1,
  updated: 0,
  unchanged: 0,
  failed: 1,
  map: [
    { legacyId: 'a', eventId: 'e-1', outcome: 'created', error: null },
    { legacyId: 'b', eventId: '', outcome: 'failed', error: 'bad dates' },
  ],
};

async function render(responder: Parameters<typeof fakeService>[0] = () => envelope(report)) {
  const service = fakeService(responder);
  const wrapper = mount(ImportPanel, { props: { client: service.client, catalog: 'ocharo' } });
  wrappers.push(wrapper);
  return { ...service, wrapper };
}

async function pick(wrapper: ReturnType<typeof mount>, content: unknown) {
  const input = wrapper.get('[data-test="import-file"]');
  const file = new File([typeof content === 'string' ? content : JSON.stringify(content)], 'bundle.json', {
    type: 'application/json',
  });
  Object.defineProperty(input.element, 'files', { value: [file], configurable: true });
  await input.trigger('change');
  await flushPromises();
}

describe('import panel', () => {
  it('refuses a file with the wrong format', async () => {
    const { wrapper } = await render();
    await pick(wrapper, { ...bundle, format: 'other.v1' });

    expect(wrapper.get('[data-test="import-file-error"]').text()).toContain('wow2.events-import.v1');
    expect(wrapper.find('[data-test="import-bundle"]').exists()).toBe(false);
    await pick(wrapper, '{not json');
    expect(wrapper.get('[data-test="import-file-error"]').text()).toContain('not valid JSON');
  });

  it('renders the dry-run counts with the failed row first', async () => {
    const { wrapper, requests } = await render();
    await pick(wrapper, bundle);
    expect(wrapper.get('[data-test="import-bundle"]').text()).toContain('afisha-v1');
    await wrapper.get('[data-test="import-dry-run"]').trigger('click');
    await flushPromises();

    const url = new URL(requests[0]!.url);
    expect(url.pathname).toBe('/api/events/v1/catalogs/ocharo/imports');
    expect(url.searchParams.get('dryRun')).toBe('true');
    expect(wrapper.get('[data-test="import-count-created"]').text()).toBe('1');
    expect(wrapper.get('[data-test="import-count-failed"]').text()).toBe('1');
    const rows = wrapper.findAll('[data-test="import-row"]');
    expect(rows[0]!.text()).toContain('bad dates');
    expect(rows[1]!.text()).toContain('created');
    expect(wrapper.get('[data-test="import-run"]').attributes('disabled')).toBeUndefined();
  });

  it('keeps Import disabled before a dry run', async () => {
    const { wrapper } = await render();
    expect(wrapper.get('[data-test="import-run"]').attributes('disabled')).toBeDefined();
    await pick(wrapper, bundle);
    expect(wrapper.get('[data-test="import-run"]').attributes('disabled')).toBeDefined();
  });
});
