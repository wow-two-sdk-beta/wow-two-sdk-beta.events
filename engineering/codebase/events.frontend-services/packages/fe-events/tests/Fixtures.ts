import { vi } from 'vitest';
import type {
  EventFacts,
  EventModel,
  EventSourceModel,
  ObservationModel,
  TrackingFacetCount,
  TrackingFacetsModel,
} from '../src/contracts';
import { createEventsClient } from '../src/kit';

export const serviceUrl = 'https://events.example.test';

/** Builds an event as the service answers it. */
export function eventModel(
  id: string,
  facts: Partial<EventFacts> = {},
  overrides: Partial<EventModel> = {},
): EventModel {
  return {
    id,
    catalog: 'ocharo',
    facts: {
      title: { und: `Event ${id}` },
      kind: 'runway',
      status: 'scheduled',
      starts: { value: null, precision: 'unknown', certainty: 'announced' },
      ...facts,
    },
    review: 'published',
    sources: [],
    pendingChanges: 0,
    revision: 1,
    createdAtUtc: '2026-11-01T10:00:00Z',
    updatedAtUtc: '2026-11-01T10:00:00Z',
    ...overrides,
  };
}

/** Builds one source identity behind an event. */
export function sourceModel(id: string, overrides: Partial<EventSourceModel> = {}): EventSourceModel {
  return {
    kind: 'scfd',
    id: `scfd/${id}`,
    url: `https://afisha.example.test/${id}`,
    name: `Source ${id}`,
    state: 'live',
    firstSeenAtUtc: '2026-11-01T10:00:00Z',
    lastSeenAtUtc: '2026-11-02T10:00:00Z',
    ...overrides,
  };
}

/** Builds an observation with its evidence. */
export function observationModel(id: string, overrides: Partial<ObservationModel> = {}): ObservationModel {
  return {
    id,
    catalog: 'ocharo',
    sourceKind: 'scfd',
    sourceInstance: 'harvest-dev',
    subscriptionId: 'sub-1',
    deliveryEventId: `delivery-${id}`,
    type: 'post.new',
    reason: 'ingest',
    postId: `post-${id}`,
    revision: 2,
    observedAtUtc: '2026-11-02T09:00:00Z',
    receivedAtUtc: '2026-11-02T09:05:00Z',
    links: [
      {
        url: 'https://afisha.example.test/fashion-week',
        sourceId: 'afisha',
        captureId: `capture-${id}`,
        capturedAtUtc: '2026-11-02T08:59:00Z',
      },
    ],
    state: 'recorded',
    payload: { title: 'Fashion week', image: 'https://afisha.example.test/poster.jpg' },
    ...overrides,
  };
}

/** Answers each request with the next responder; records every request. */
export function fakeService(...responders: ((request: Request) => Response | Promise<Response>)[]) {
  const requests: Request[] = [];
  const fetch = vi.fn<typeof globalThis.fetch>(async (input, init) => {
    const request = new Request(input, init);
    requests.push(request);
    const responder = responders.length > 1 ? responders.shift() : responders[0];
    return responder ? responder(request) : Response.json({ data: null });
  });
  return { fetch, requests, client: createEventsClient({ baseUrl: serviceUrl, fetch, csrfToken: () => 'csrf-1' }) };
}

/** Wraps `data` in the contract's envelope. */
export const envelope = (data: unknown, init?: ResponseInit) => Response.json({ data }, init);

/** Builds one facet entry: `value`, how many events it lists and, for a source, its name as stored. */
export const facetCount = (value: string, count: number, label?: string): TrackingFacetCount => ({
  value,
  count,
  ...(label ? { label } : {}),
});

/** Builds the facets answer for `total` events, each facet overridable. */
export function facetsModel(total: number, overrides: Partial<TrackingFacetsModel> = {}): TrackingFacetsModel {
  return {
    total,
    countries: [facetCount('UZ', 1), facetCount('KZ', 1)],
    cities: [facetCount('tashkent', 1), facetCount('almaty', 1)],
    kinds: [facetCount('runway', 2), facetCount('exhibition', 0)],
    stages: [
      facetCount('candidate', 2),
      facetCount('shortlisted', 1),
      facetCount('skipped', 2),
      facetCount('rejected', 0),
    ],
    priorities: [facetCount('high', 1), facetCount('normal', 1), facetCount('low', 0)],
    sources: [facetCount('afisha', 1, 'Afisha'), facetCount('curated', 1)],
    openDeadline: { before: '2026-12-10', count: 1 },
    ...overrides,
  };
}

/**
 * Replaces `IntersectionObserver` with one that reports nothing until `fire()` says the sentinel scrolled into view;
 * `fire()` reports to every observer still watching an element. Undo it with `vi.unstubAllGlobals()`.
 */
export function stubIntersectionObserver() {
  const live = new Set<{ callback: IntersectionObserverCallback; targets: Element[] }>();
  class FakeObserver {
    private readonly record: { callback: IntersectionObserverCallback; targets: Element[] };
    constructor(callback: IntersectionObserverCallback) {
      this.record = { callback, targets: [] };
      live.add(this.record);
    }
    observe(target: Element) {
      this.record.targets.push(target);
    }
    unobserve() {}
    disconnect() {
      live.delete(this.record);
    }
    takeRecords() {
      return [];
    }
  }
  vi.stubGlobal('IntersectionObserver', FakeObserver);
  return {
    watching: () => [...live].filter((record) => record.targets.length > 0).length,
    fire() {
      for (const record of [...live]) {
        const entries = record.targets.map((target) => ({ isIntersecting: true, target }) as IntersectionObserverEntry);
        if (entries.length) record.callback(entries, record as unknown as IntersectionObserver);
      }
    },
  };
}
