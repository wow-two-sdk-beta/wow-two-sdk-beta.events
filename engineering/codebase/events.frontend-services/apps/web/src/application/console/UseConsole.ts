import type { CatalogModel } from '@wow-two-beta/fe-events/contracts';
import { toEventsLocale, type EventsClient, type EventsFailure, type EventsLocale } from '@wow-two-beta/fe-events/kit';
import {
  computed,
  inject,
  onMounted,
  onScopeDispose,
  provide,
  shallowRef,
  watch,
  type ComputedRef,
  type InjectionKey,
  type ShallowRef,
} from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { catalogQueryParameter } from '../../domain/console/ConsolePaths';
import { hasPermission, signInUrl, type ConsoleSession } from '../../domain/session/ConsoleSession';
import { createConsoleEventsClient } from '../../integration/events';
import { sessionApi } from '../../integration/session';

/** Holds what every console screen shares: the session, the Events client, the catalogs and the chosen one. */
export interface ConsoleState {
  readonly session: ShallowRef<ConsoleSession | null>;
  readonly sessionFailed: ShallowRef<boolean>;
  readonly client: EventsClient;
  readonly catalogs: ShallowRef<readonly CatalogModel[] | null>;
  readonly catalogsFailure: ShallowRef<EventsFailure | null>;
  readonly catalog: ComputedRef<CatalogModel | null>;
  readonly locale: ShallowRef<EventsLocale>;
  readonly canReview: ComputedRef<boolean>;
  selectCatalog(slug: string): void;
  /** The BFF's sign-in address, returning to the current page. */
  signInHref(): string;
  signOut(): Promise<void>;
  reloadSession(): Promise<void>;
}

const ConsoleKey: InjectionKey<ConsoleState> = Symbol('EventsConsole');

const queryText = (value: unknown) =>
  typeof value === 'string' ? value : Array.isArray(value) && typeof value[0] === 'string' ? value[0] : null;

/** Creates the console's shared state for the app's lifetime and provides it to every page. */
export function provideConsole(navigate: (url: string) => void = (url) => window.location.assign(url)): ConsoleState {
  const route = useRoute();
  const router = useRouter();
  const controller = new AbortController();
  const session = shallowRef<ConsoleSession | null>(null);
  const sessionFailed = shallowRef(false);
  const catalogs = shallowRef<readonly CatalogModel[] | null>(null);
  const catalogsFailure = shallowRef<EventsFailure | null>(null);
  const selected = shallowRef<string | null>(queryText(route.query[catalogQueryParameter]));
  const locale = shallowRef<EventsLocale>(toEventsLocale(typeof navigator === 'undefined' ? 'en' : navigator.language));
  const client = createConsoleEventsClient(
    () => (session.value?.isAuthenticated ? session.value.csrfToken : null),
    () => void reloadSession(),
  );

  const catalog = computed(
    () => catalogs.value?.find((item) => item.slug === selected.value) ?? catalogs.value?.[0] ?? null,
  );
  const canReview = computed(() => hasPermission(session.value, 'events:review'));

  async function loadCatalogs() {
    const result = await client.listCatalogs(controller.signal);
    if (controller.signal.aborted) return;
    catalogs.value = result.ok ? result.value : [];
    catalogsFailure.value = result.ok ? null : result.failure;
  }

  async function reloadSession() {
    const result = await sessionApi.getSession(controller.signal);
    if (controller.signal.aborted) return;
    sessionFailed.value = !result.ok;
    session.value = result.ok ? result.value : null;
    if (result.ok && result.value.isAuthenticated) await loadCatalogs();
  }

  function selectCatalog(slug: string) {
    selected.value = slug;
    void router.replace({ query: { ...route.query, [catalogQueryParameter]: slug } });
  }

  async function signOut() {
    const current = session.value;
    if (!current?.isAuthenticated) return;
    const result = await sessionApi.signOut(current.csrfToken);
    session.value = { isAuthenticated: false };
    catalogs.value = null;
    if (result.ok) navigate(result.value);
  }

  watch(
    () => queryText(route.query[catalogQueryParameter]),
    (slug) => {
      if (slug) selected.value = slug;
    },
  );
  onMounted(reloadSession);
  onScopeDispose(() => controller.abort());

  const state: ConsoleState = {
    session,
    sessionFailed,
    client,
    catalogs,
    catalogsFailure,
    catalog,
    locale,
    canReview,
    selectCatalog,
    signInHref: () => signInUrl(route.fullPath),
    signOut,
    reloadSession,
  };
  provide(ConsoleKey, state);
  return state;
}

/** Reads the console's shared state inside a page. */
export function useConsole(): ConsoleState {
  const state = inject(ConsoleKey, null);
  if (!state) throw new Error('useConsole() needs provideConsole() in an ancestor.');
  return state;
}
