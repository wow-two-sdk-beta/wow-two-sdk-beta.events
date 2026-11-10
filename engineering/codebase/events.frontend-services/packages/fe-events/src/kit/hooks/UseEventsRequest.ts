import { onScopeDispose, shallowRef, type ShallowRef } from 'vue';
import type { EventsResult } from '../client/CreateEventsClient';
import type { EventsFailure } from '../client/EventsFailure';

/** Holds one request's state for a component's lifetime: the last answer, the last failure, and whether one runs. */
export interface EventsRequest<T, A extends unknown[]> {
  readonly value: ShallowRef<T | null>;
  readonly failure: ShallowRef<EventsFailure | null>;
  readonly pending: ShallowRef<boolean>;
  /** Runs the request, aborting the one before; resolves the answer, or null on failure or abort. */
  run(...args: A): Promise<T | null>;
}

/**
 * Runs a client call with abort-on-rerun and abort-on-unmount; a cancelled call changes nothing. Used by every kit and
 * admin screen, so each one takes a client prop and keeps no global state.
 */
export function useEventsRequest<T, A extends unknown[] = []>(
  call: (signal: AbortSignal, ...args: A) => EventsResult<T>,
): EventsRequest<T, A> {
  const value = shallowRef<T | null>(null);
  const failure = shallowRef<EventsFailure | null>(null);
  const pending = shallowRef(false);
  let controller: AbortController | null = null;

  async function run(...args: A): Promise<T | null> {
    controller?.abort();
    const current = new AbortController();
    controller = current;
    pending.value = true;
    const result = await call(current.signal, ...args);
    if (current.signal.aborted) return null;
    pending.value = false;
    if (result.ok) {
      value.value = result.value;
      failure.value = null;
      return result.value;
    }
    if (result.failure.reason !== 'cancelled') failure.value = result.failure;
    return null;
  }

  onScopeDispose(() => controller?.abort());
  return { value, failure, pending, run };
}
