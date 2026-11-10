import { flushPromises, mount } from '@vue/test-utils';
import { RouterHistoryMode } from '@wow-two-beta/fe/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import AppRoot from '../src/bootstrap/AppRoot.vue';
import { createWebRouter } from '../src/bootstrap/router/Routes';

const wrappers: ReturnType<typeof mount>[] = [];

afterEach(() => {
  for (const wrapper of wrappers.splice(0)) wrapper.unmount();
  vi.unstubAllGlobals();
});

const signedIn = {
  isAuthenticated: true,
  person: { sub: 'p1', name: 'Malika Karimova', email: 'malika@ocharo.test', phoneNumber: null },
  organization: { id: '0193aa00-0000-7000-8000-000000000001', name: 'Ocharo', slug: 'ocharo', role: 'owner' },
  permissions: ['events:read', 'events:review'],
  csrfToken: 'csrf-1',
  expiresAt: '2026-11-11T00:00:00Z',
};
const catalogs = [
  {
    slug: 'ocharo',
    name: 'Ocharo showcase',
    ownerIssuer: 'https://id.ocharo.test',
    ownerOrganizationId: signedIn.organization.id,
    visibility: 'private',
    kinds: [{ key: 'runway', label: { en: 'Runway show' } }],
    revision: 1,
    createdAtUtc: '2026-11-01T00:00:00Z',
    updatedAtUtc: '2026-11-01T00:00:00Z',
  },
];
const candidate = {
  id: 'c1',
  catalog: 'ocharo',
  facts: {
    title: { und: 'Tashkent Fashion Week' },
    kind: 'runway',
    starts: { value: '2026-10', precision: 'month', certainty: 'estimated' },
  },
  review: 'candidate',
  revision: 1,
  createdAtUtc: '2026-11-01T00:00:00Z',
  updatedAtUtc: '2026-11-01T00:00:00Z',
};

type Answer = (url: URL, request: Request) => Response | undefined;

/** Answers the console's same-origin calls: status, the BFF session, the catalogs, then each screen's route. */
function serve(session: unknown, ...answers: Answer[]) {
  const requests: Request[] = [];
  const fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(
      new URL(input instanceof Request ? input.url : String(input), 'https://events.test'),
      init,
    );
    requests.push(request);
    const url = new URL(request.url);
    for (const answer of answers) {
      const response = answer(url, request);
      if (response) return response;
    }
    switch (url.pathname) {
      case '/api/system/status':
        return Response.json({ data: { service: 'events', status: 'ok', version: '0.1.4+abc1234' } });
      case '/api/identity/session':
        return Response.json({ data: session });
      case '/api/events/v1/catalogs':
        return Response.json({ data: catalogs });
      case '/api/events/v1/catalogs/ocharo/review':
        return Response.json({ data: { items: [{ event: candidate, observations: [] }], nextCursor: null } });
      case '/api/events/v1/catalogs/ocharo/observations':
        return Response.json({ data: { items: [], nextCursor: null } });
      case '/api/events/v1/catalogs/ocharo/events':
        return Response.json({ data: { items: [candidate], nextCursor: null } });
      case '/api/events/v1/intake':
        return Response.json({ data: { sources: [], candidates: 3, pendingChanges: 1 } });
      default:
        return Response.json({ title: 'Not found' }, { status: 404 });
    }
  });
  vi.stubGlobal('fetch', fetch);
  return requests;
}

/** Mounts the app at `path`, routed as in the browser but over memory history. */
async function renderAt(path: string) {
  const router = createWebRouter(RouterHistoryMode.Memory);
  await router.push(path);
  await router.isReady();
  const wrapper = mount(AppRoot, { global: { plugins: [router] } });
  wrappers.push(wrapper);
  await flushPromises();
  await flushPromises();
  return wrapper;
}

describe('sign-in', () => {
  it('offers the BFF sign-in back to the page when there is no session', async () => {
    const requests = serve({ isAuthenticated: false });
    const wrapper = await renderAt('/observations');

    expect(wrapper.get('[data-test="sign-in"]').attributes('href')).toBe(
      '/api/identity/sign-in?returnUrl=%2Fobservations',
    );
    expect(wrapper.find('[data-test="sign-in-panel"]').exists()).toBe(true);
    expect(requests.some((request) => request.url.includes('/api/events/'))).toBe(false);
  });

  it('shows the person, the active organization and the running versions', async () => {
    serve(signedIn);
    const wrapper = await renderAt('/');

    expect(wrapper.get('[data-test="session"]').text()).toBe('Malika Karimova · Ocharo');
    expect(wrapper.get('select[name="catalog"]').text()).toContain('Ocharo showcase');
    expect(wrapper.get('[aria-label="App version"]').text()).toBe('app test');
    expect(wrapper.get('[aria-label="API version"]').text()).toBe('api 0.1.4+abc1234');
  });

  it('warns when the role lacks events:review, and the queue says the same on a 403', async () => {
    serve({ ...signedIn, permissions: ['events:read'] }, (url) =>
      url.pathname.endsWith('/review') ? Response.json({ title: 'Forbidden' }, { status: 403 }) : undefined,
    );
    const wrapper = await renderAt('/');

    expect(wrapper.get('[data-test="missing-review"]').text()).toContain('events:review');
    expect(wrapper.get('[data-reason="forbidden"]').text()).toContain('events:review');
  });

  it('checks the session again when the API answers 401, and offers the sign-in once it ended', async () => {
    let sessions = 0;
    const requests = serve(signedIn, (url) => {
      if (url.pathname === '/api/identity/session')
        return Response.json({ data: sessions++ === 0 ? signedIn : { isAuthenticated: false } });
      return url.pathname.endsWith('/review') ? Response.json({ title: 'SignInRequired' }, { status: 401 }) : undefined;
    });
    const wrapper = await renderAt('/');
    await flushPromises();

    expect(requests.filter((request) => request.url.endsWith('/api/identity/session'))).toHaveLength(2);
    expect(wrapper.find('[data-test="sign-in-panel"]').exists()).toBe(true);
  });
});

describe('console routes', () => {
  it('renders the review queue at the root for the chosen catalog', async () => {
    const requests = serve(signedIn);
    const wrapper = await renderAt('/?catalog=ocharo');

    expect(wrapper.get('[data-test="review-item"]').text()).toContain('Tashkent Fashion Week');
    expect(requests.some((request) => new URL(request.url).pathname === '/api/events/v1/catalogs/ocharo/review')).toBe(
      true,
    );
  });

  it('accepts with the session antiforgery token', async () => {
    const requests = serve(signedIn, (url, request) =>
      request.method === 'POST' && url.pathname.endsWith('/accept')
        ? Response.json({ data: { ...candidate, review: 'published', revision: 2 } })
        : undefined,
    );
    const wrapper = await renderAt('/');

    [...wrapper.element.querySelectorAll('button')].find((item) => item.textContent?.trim() === 'Accept')?.click();
    await flushPromises();

    const accept = requests.find((request) => request.method === 'POST')!;
    expect(new URL(accept.url).pathname).toBe('/api/events/v1/catalogs/ocharo/review/c1/accept');
    expect(accept.headers.get('X-Csrf-Token')).toBe('csrf-1');
  });

  it('renders observations, catalog events and intake', async () => {
    serve(signedIn);

    expect((await renderAt('/observations')).get('[data-filter="quarantined"]').text()).toBe('Quarantined');
    expect((await renderAt('/events')).get('[data-test="catalog-event-row"]').text()).toContain(
      'Tashkent Fashion Week',
    );
    expect((await renderAt('/intake')).get('[data-test="intake-candidates"]').text()).toBe('3');
  });
});
