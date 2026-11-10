import { createEventsClient, type EventsClient } from '@wow-two-beta/fe-events/kit';

/**
 * Creates the console's Events client: same-origin `/api/events/v1/...` with the BFF session cookie, echoing the
 * session's antiforgery token on unsafe requests; a 401 tells the console to check the session again.
 */
export function createConsoleEventsClient(csrfToken: () => string | null, onSignInRequired: () => void): EventsClient {
  return createEventsClient({ csrfToken, onSignInRequired });
}
