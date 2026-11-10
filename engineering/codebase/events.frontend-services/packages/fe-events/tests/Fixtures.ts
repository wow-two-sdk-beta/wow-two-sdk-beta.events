import { vi } from 'vitest';
import type { EventFacts, EventModel, ObservationModel } from '../src/contracts';
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
