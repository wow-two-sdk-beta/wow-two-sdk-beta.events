/** Holds the console's routes: the review queue at the root, then observations, catalog events, intake, imports and digests. */
export const ConsolePaths = {
  review: '/',
  observations: '/observations',
  events: '/events',
  intake: '/intake',
  imports: '/imports',
  digests: '/digests',
} as const;

/** Names the query parameter that keeps the chosen catalog in the address, so a link opens the same catalog. */
export const catalogQueryParameter = 'catalog';
