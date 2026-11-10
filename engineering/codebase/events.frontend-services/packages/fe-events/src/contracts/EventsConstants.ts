/** Mirrors `EventsConstants` of the be contract: its limits and formats; the server stays authoritative. */
export const EventsConstants = {
  keyPattern: '^[a-z0-9][a-z0-9-]{0,63}$',
  maxKeyLength: 64,
  maxTitleLength: 200,
  maxSummaryLength: 2_000,
  maxNameLength: 200,
  maxUrlLength: 2_048,
  maxNotesLength: 8_000,
  maxTextLength: 2_000,
  maxListLength: 50,
  defaultPageSize: 50,
  maxPageSize: 200,
  maxImportRows: 5_000,
  unknownLocale: 'und',
  importFormat: 'wow2.events-import.v1',
  eventSchema: 'wow2.event.v1',
} as const;

/** Mirrors `EventsScopeConstants`: the OAuth scopes an app holds. */
export const EventsScopeConstants = {
  read: 'events.read',
  track: 'events.track',
  review: 'events.review',
  import: 'events.import',
  admin: 'events.admin',
} as const;

/** Mirrors `EventsPermissionConstants`: the permissions a person holds in an organization, beside the app's scope. */
export const EventsPermissionConstants = {
  read: 'events:read',
  track: 'events:track',
  review: 'events:review',
  admin: 'events:admin',
} as const;
