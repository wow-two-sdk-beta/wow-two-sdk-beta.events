import { createAppRouter, lazyRoute, RouterHistoryMode, type RouteConfig } from '@wow-two-beta/fe/router';
import { ConsolePaths } from '../../domain/console/ConsolePaths';

/** Holds the console's route table: the review queue at the root, then observations, catalog events, intake and imports. */
export const routes: RouteConfig = [
  {
    path: ConsolePaths.review,
    id: 'review',
    handle: { title: 'Review' },
    lazy: lazyRoute(() => import('../../presentation/review/pages/ReviewPage.vue')),
  },
  {
    path: ConsolePaths.observations,
    id: 'observations',
    handle: { title: 'Observations' },
    lazy: lazyRoute(() => import('../../presentation/observations/pages/ObservationsPage.vue')),
  },
  {
    path: ConsolePaths.events,
    id: 'events',
    handle: { title: 'Events' },
    lazy: lazyRoute(() => import('../../presentation/events/pages/EventsPage.vue')),
  },
  {
    path: ConsolePaths.intake,
    id: 'intake',
    handle: { title: 'Intake' },
    lazy: lazyRoute(() => import('../../presentation/intake/pages/IntakePage.vue')),
  },
  {
    path: ConsolePaths.imports,
    id: 'imports',
    handle: { title: 'Imports' },
    lazy: lazyRoute(() => import('../../presentation/imports/pages/ImportsPage.vue')),
  },
];

/** Creates the app's router over `history`: the browser's in the app, memory in tests. */
export function createWebRouter(history: RouterHistoryMode = RouterHistoryMode.Browser) {
  return createAppRouter(routes, { titleSuffix: 'Events', routePersistence: false, history });
}
