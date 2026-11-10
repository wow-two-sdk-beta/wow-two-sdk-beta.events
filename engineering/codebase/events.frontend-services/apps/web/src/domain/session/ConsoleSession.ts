/** Holds the routes Identity's BFF serves for the console, under its default prefix `/api/identity`. */
export const SessionRouteConstants = {
  session: '/api/identity/session',
  signIn: '/api/identity/sign-in',
  signOut: '/api/identity/sign-out',
  returnUrlParameter: 'returnUrl',
  csrfHeader: 'X-Csrf-Token',
} as const;

/** Represents the signed-in person, as the BFF's session route answers it. */
export interface ConsolePerson {
  readonly sub: string;
  readonly name: string | null;
  readonly email: string | null;
  readonly picture?: string | null;
}

/** Represents the session's active organization and the person's role in it. */
export interface ConsoleOrganization {
  readonly id: string;
  readonly name: string | null;
  readonly slug: string | null;
  readonly role: string | null;
}

/** Represents a live BFF session: who, in which organization, with which permissions and antiforgery token. */
export interface SignedInSession {
  readonly isAuthenticated: true;
  readonly person: ConsolePerson;
  readonly organization: ConsoleOrganization | null;
  /** The active organization's permissions without the `{org_id}:` prefix, such as `events:review`. */
  readonly permissions: readonly string[];
  readonly csrfToken: string;
  readonly expiresAt: string;
}

/** Represents the console's session: signed in, or not. */
export type ConsoleSession = SignedInSession | { readonly isAuthenticated: false };

/** Names a person: the display name, else the email, else the subject. */
export function personLabel(person: ConsolePerson): string {
  return person.name?.trim() || person.email?.trim() || person.sub;
}

/** Names an organization: its name, else its handle, else its id. */
export function organizationLabel(organization: ConsoleOrganization): string {
  return organization.name?.trim() || organization.slug?.trim() || organization.id;
}

/** Says whether the session holds `permission` in its active organization. */
export function hasPermission(session: ConsoleSession | null, permission: string): boolean {
  return session?.isAuthenticated === true && session.permissions.includes(permission);
}

/** Builds the BFF's sign-in address that returns to `returnUrl`, a path on this origin. */
export function signInUrl(returnUrl: string): string {
  const path = returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/';
  return `${SessionRouteConstants.signIn}?${SessionRouteConstants.returnUrlParameter}=${encodeURIComponent(path)}`;
}
