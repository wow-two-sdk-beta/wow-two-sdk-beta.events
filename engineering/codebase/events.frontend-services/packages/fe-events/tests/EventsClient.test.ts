import { describe, expect, it, vi } from 'vitest';
import { EventsRoutes, eventsQueryString } from '../src/contracts';
import { createEventsClient } from '../src/kit';
import { envelope, eventModel, fakeService, serviceUrl } from './Fixtures';

const problem =
  (status: number, body: Record<string, unknown> = { title: 'Problem' }) =>
  () =>
    Response.json(body, { status, headers: { 'Content-Type': 'application/problem+json' } });

describe('routes', () => {
  it('builds every route relative to the service root, slugs and ids encoded', () => {
    expect(EventsRoutes.catalogs()).toBe('api/events/v1/catalogs');
    expect(EventsRoutes.events('ocharo hq')).toBe('api/events/v1/catalogs/ocharo%20hq/events');
    expect(EventsRoutes.candidates('ocharo')).toBe('api/events/v1/catalogs/ocharo/candidates');
    expect(EventsRoutes.accept('ocharo', 'e/1')).toBe('api/events/v1/catalogs/ocharo/review/e%2F1/accept');
    expect(EventsRoutes.applyChange('ocharo', 'o1')).toBe('api/events/v1/catalogs/ocharo/observations/o1/apply');
    expect(EventsRoutes.trackingSummary('ocharo')).toBe('api/events/v1/catalogs/ocharo/tracking/summary');
    expect(EventsRoutes.trackingFacets('ocharo hq')).toBe('api/events/v1/catalogs/ocharo%20hq/tracking/facets');
    expect(EventsRoutes.calendar('ocharo')).toBe('api/events/v1/catalogs/ocharo/events.ics');
    expect(EventsRoutes.intakePoll()).toBe('api/events/v1/intake/poll');
    expect(EventsRoutes.freshness('ocharo hq')).toBe('api/events/v1/catalogs/ocharo%20hq/freshness');
    expect(eventsQueryString({ from: '2026-01-01', text: '', limit: undefined, includeUndated: false })).toBe(
      '?from=2026-01-01&includeUndated=false',
    );
  });

  it('calls the routes under the base URL with the query names of the contract', async () => {
    const { requests, fetch } = fakeService(() => envelope({ items: [], nextCursor: null }));
    const client = createEventsClient({ baseUrl: `${serviceUrl}/api/proxy/`, fetch });

    await client.listEvents('ocharo hq', {
      from: '2026-01-01',
      kind: 'runway',
      review: 'published',
      includeUndated: false,
      cursor: 'abc',
      limit: 20,
    });

    const url = new URL(requests[0]!.url);
    expect(url.origin + url.pathname).toBe(`${serviceUrl}/api/proxy/api/events/v1/catalogs/ocharo%20hq/events`);
    expect(Object.fromEntries(url.searchParams)).toEqual({
      from: '2026-01-01',
      kind: 'runway',
      review: 'published',
      includeUndated: 'false',
      cursor: 'abc',
      limit: '20',
    });
    expect(requests[0]!.method).toBe('GET');
    expect(requests[0]!.headers.get('X-Csrf-Token')).toBeNull();
  });
});

describe('answers', () => {
  it('unwraps the data envelope', async () => {
    const event = eventModel('e1');
    const { client } = fakeService(() => envelope(event));

    const result = await client.getEvent('ocharo', 'e1');

    expect(result).toEqual({ ok: true, value: event });
  });

  it('refuses an answer without the envelope or of the wrong shape as a protocol failure', async () => {
    const { client } = fakeService(
      () => Response.json(eventModel('e1')),
      () => envelope({ id: 42 }),
    );

    const bare = await client.getEvent('ocharo', 'e1');
    const malformed = await client.getEvent('ocharo', 'e1');

    expect(!bare.ok && bare.failure.reason).toBe('protocol');
    expect(!malformed.ok && malformed.failure.reason).toBe('protocol');
  });

  it('reads the calendar as text, without an envelope', async () => {
    const { client, requests } = fakeService(
      () => new Response('BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n', { headers: { 'Content-Type': 'text/calendar' } }),
    );

    const result = await client.exportCalendar('ocharo', { country: 'UZ' });

    expect(result).toEqual({ ok: true, value: 'BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n' });
    expect(new URL(requests[0]!.url).pathname).toBe('/api/events/v1/catalogs/ocharo/events.ics');
    expect(new URL(requests[0]!.url).searchParams.get('country')).toBe('UZ');
    expect(requests[0]!.headers.get('Accept')).toBe('text/calendar');
  });
});

describe('writes', () => {
  it('proposes an event as a candidate, echoing the session antiforgery token', async () => {
    const candidate = eventModel('c1', {}, { review: 'candidate' });
    const { client, requests } = fakeService(() => envelope(candidate, { status: 201 }));

    const result = await client.proposeEvent('ocharo', { facts: { title: { und: 'Fashion week' } } });

    expect(result.ok && result.value.review).toBe('candidate');
    expect(requests[0]!.method).toBe('POST');
    expect(new URL(requests[0]!.url).pathname).toBe('/api/events/v1/catalogs/ocharo/candidates');
    expect(requests[0]!.headers.get('X-Csrf-Token')).toBe('csrf-1');
    expect(await requests[0]!.json()).toEqual({ facts: { title: { und: 'Fashion week' } } });
  });

  it('names the expected revision in the query of a reject and an archive', async () => {
    const { client, requests } = fakeService(() => envelope(eventModel('e1')));

    await client.rejectEvent('ocharo', 'e1', 3);
    await client.archiveEvent('ocharo', 'e1', 4);

    expect(requests[0]!.method).toBe('POST');
    expect(requests[0]!.url).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo/review/e1/reject?expectedRevision=3`);
    expect(requests[1]!.method).toBe('DELETE');
    expect(requests[1]!.url).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo/events/e1?expectedRevision=4`);
  });

  it('names the expected revision in the query of a reopen', async () => {
    const { client, requests } = fakeService(() => envelope(eventModel('e1', {}, { review: 'candidate' })));

    const result = await client.reopenEvent('ocharo', 'e1', 6);

    expect(result.ok && result.value.review).toBe('candidate');
    expect(requests[0]!.method).toBe('POST');
    expect(requests[0]!.url).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo/review/e1/reopen?expectedRevision=6`);
  });

  it('splits a source off an event with the source and the revision read, and answers the new candidate', async () => {
    const { client, requests } = fakeService(() => envelope(eventModel('e2', {}, { review: 'candidate' })));

    const result = await client.splitSource('ocharo', 'e1', {
      kind: 'scfd',
      sourceId: 'scfd/post-2',
      expectedRevision: 7,
    });

    expect(result.ok && result.value.id).toBe('e2');
    expect(requests[0]!.method).toBe('POST');
    expect(requests[0]!.url).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo/review/e1/split`);
    expect(await requests[0]!.json()).toEqual({ kind: 'scfd', sourceId: 'scfd/post-2', expectedRevision: 7 });
  });

  it('sends an import bundle with its legacy rows and the dry-run flag', async () => {
    const report = { dryRun: true, source: 'ocharo-desk', created: 1, updated: 0, unchanged: 0, failed: 0, map: [] };
    const { client, requests } = fakeService(() => envelope(report));
    const bundle = {
      source: 'ocharo-desk',
      rows: [{ legacyId: 'l1', facts: { title: { und: 'Expo' } }, legacy: { status: 'shortlisted', cost: null } }],
    };

    const result = await client.importEvents('ocharo', bundle, true);

    expect(result).toEqual({ ok: true, value: report });
    expect(requests[0]!.url).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo/imports?dryRun=true`);
    expect(await requests[0]!.json()).toEqual(bundle);
  });

  it('asks the tracking summary for the given day', async () => {
    const { client, requests } = fakeService(() => envelope({ byStage: {}, upcomingDeadlines: [] }));

    await client.getTrackingSummary('ocharo', '2026-11-10');

    expect(requests[0]!.url).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo/tracking/summary?today=2026-11-10`);
  });
});

describe('tracking list and facets (UI04)', () => {
  const facets = {
    total: 3,
    countries: [{ value: 'UZ', count: 2 }],
    cities: [{ value: 'tashkent', count: 2 }],
    kinds: [
      { value: 'fair', count: 3 },
      { value: 'conference', count: 0 },
    ],
    stages: [{ value: 'interested', count: 3 }],
    priorities: [{ value: 'high', count: 1 }],
    sources: [
      { value: 'afisha', count: 2, label: 'Afisha' },
      { value: 'curated', count: 1 },
    ],
    openDeadline: { before: '2026-12-10', count: 1 },
  };

  it('sends the new list filters as repeated keys and the sort only when it is not the default', async () => {
    const { client, requests } = fakeService(() => envelope({ items: [], nextCursor: null }));

    await client.listTracking('ocharo', {
      kinds: ['exhibition', 'fair'],
      city: 'bukhara',
      cities: ['tashkent', 'samarkand'],
      status: 'movedOnline',
      sources: ['afisha', 'curated'],
      publishedSince: '2026-11-10T09:00:00Z',
      sort: 'added',
      cursor: 'next',
      limit: 20,
    });
    await client.listTracking('ocharo', { sort: 'starts', cities: [], sources: [] });
    await client.listTracking('ocharo', { sort: 'deadline' });

    expect(requests.map((request) => request.url)).toEqual([
      `${serviceUrl}/api/events/v1/catalogs/ocharo/tracking?kinds=exhibition&kinds=fair&city=bukhara&cities=tashkent&cities=samarkand&status=movedOnline&sources=afisha&sources=curated&publishedSince=2026-11-10T09%3A00%3A00Z&sort=added&cursor=next&limit=20`,
      `${serviceUrl}/api/events/v1/catalogs/ocharo/tracking`,
      `${serviceUrl}/api/events/v1/catalogs/ocharo/tracking?sort=deadline`,
    ]);
  });

  it("asks the facets with the list's filters, repeated keys and no sort, cursor or limit", async () => {
    const { client, requests } = fakeService(() => envelope(facets));

    await client.getTrackingFacets('ocharo hq', {
      cities: ['tashkent'],
      sources: ['afisha', 'curated'],
      excludeStages: ['skipped'],
      deadlineBefore: '2026-12-31',
      sort: 'added',
      cursor: 'ignored',
      limit: 5,
    });
    await client.getTrackingFacets('ocharo');

    const url = new URL(requests[0]!.url);
    expect(requests[0]!.method).toBe('GET');
    expect(url.origin + url.pathname).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo%20hq/tracking/facets`);
    expect(url.search).toBe(
      '?excludeStages=skipped&cities=tashkent&sources=afisha&sources=curated&deadlineBefore=2026-12-31',
    );
    expect(requests[1]!.url).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo/tracking/facets`);
  });

  it('unwraps the facets as the service writes them, a label only on a source', async () => {
    const { client } = fakeService(() => envelope(facets));

    const result = await client.getTrackingFacets('ocharo', { kinds: ['fair'] });

    expect(result).toEqual({ ok: true, value: facets });
  });

  it('refuses facets with no total, a malformed count or no deadline window as a protocol failure', async () => {
    const { client } = fakeService(
      () => envelope({ ...facets, total: undefined }),
      () => envelope({ ...facets, cities: [{ value: 'tashkent', count: '2' }] }),
      () => envelope({ ...facets, openDeadline: undefined }),
    );

    const noTotal = await client.getTrackingFacets('ocharo');
    const badCount = await client.getTrackingFacets('ocharo');
    const noWindow = await client.getTrackingFacets('ocharo');

    for (const result of [noTotal, badCount, noWindow]) expect(!result.ok && result.failure.reason).toBe('protocol');
  });

  it('maps a refused query, such as a cursor of another sort, to a validation failure, and a hidden catalog to notFound', async () => {
    const { client } = fakeService(problem(400, { title: 'The list cursor is not valid.' }), problem(404));

    const refused = await client.listTracking('ocharo', { sort: 'added', cursor: 'from-another-sort' });
    const hidden = await client.getTrackingFacets('ocharo');

    expect(!refused.ok && refused.failure.reason).toBe('validation');
    expect(!hidden.ok && hidden.failure.reason).toBe('notFound');
  });
});

describe('intake freshness', () => {
  it("reads a catalog's freshness from its own route and unwraps it", async () => {
    const freshness = {
      lastReceivedAtUtc: '2026-11-20T08:00:00Z',
      newestObservedAtUtc: '2026-11-20T07:40:00Z',
      failingSources: 1,
    };
    const { client, requests } = fakeService(() => envelope(freshness));

    const result = await client.getIntakeFreshness('ocharo');

    expect(requests[0]!.method).toBe('GET');
    expect(requests[0]!.url).toBe(`${serviceUrl}/api/events/v1/catalogs/ocharo/freshness`);
    expect(result).toEqual({ ok: true, value: freshness });
  });

  it('accepts a catalog that has received nothing: no times, no failing source', async () => {
    const { client } = fakeService(() => envelope({ lastReceivedAtUtc: null, failingSources: 0 }));

    const result = await client.getIntakeFreshness('ocharo');

    expect(result.ok).toBe(true);
  });

  it("refuses an answer without the count as a protocol failure, and maps another organization's 404 to notFound", async () => {
    const { client } = fakeService(() => envelope({ lastReceivedAtUtc: null }), problem(404));

    const malformed = await client.getIntakeFreshness('ocharo');
    const hidden = await client.getIntakeFreshness('ocharo');

    expect(!malformed.ok && malformed.failure.reason).toBe('protocol');
    expect(!hidden.ok && hidden.failure.reason).toBe('notFound');
  });
});

describe('failures', () => {
  it.each([
    [400, 'validation'],
    [401, 'signInRequired'],
    [403, 'forbidden'],
    [404, 'notFound'],
    [409, 'conflict'],
    [503, 'unavailable'],
    [500, 'unexpected'],
  ] as const)('maps status %i to %s', async (status, reason) => {
    const { client } = fakeService(problem(status));

    const result = await client.listReview('ocharo');

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.failure.reason).toBe(reason);
      expect(result.failure.status).toBe(status);
    }
  });

  it('keeps the problem detail and the field errors of a validation failure', async () => {
    const { client } = fakeService(
      problem(400, { title: 'Validation', detail: 'The title is required.', errors: { 'facts.title': ['Required.'] } }),
    );

    const result = await client.createEvent('ocharo', { facts: { title: {} } });

    expect(!result.ok && result.failure.detail).toBe('The title is required.');
    expect(!result.ok && result.failure.fieldErrors).toEqual({ 'facts.title': ['Required.'] });
  });

  it('tells the host about a 401 so it can check its session', async () => {
    const onSignInRequired = vi.fn();
    const { fetch } = fakeService(problem(401));

    await createEventsClient({ fetch, onSignInRequired }).listCatalogs();

    expect(onSignInRequired).toHaveBeenCalledWith(expect.objectContaining({ reason: 'signInRequired', status: 401 }));
  });

  it('reports an unreachable service as unavailable', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>(async () => {
      throw new TypeError('Failed to fetch');
    });

    const result = await createEventsClient({ fetch }).getIntakeStatus();

    expect(!result.ok && result.failure.reason).toBe('unavailable');
  });
});
