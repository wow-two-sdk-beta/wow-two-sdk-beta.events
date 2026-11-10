import { ApiFailureFactory, createApiClient, type ApiFailure } from '@wow-two-beta/fe/foundation/http';
import { ResultExtensions, type Result } from '@wow-two-beta/fe/foundation/results';
import {
  SessionRouteConstants,
  type ConsoleOrganization,
  type ConsolePerson,
  type ConsoleSession,
} from '../../domain/session/ConsoleSession';

const client = createApiClient();

type Json = Readonly<Record<string, unknown>>;
const isObject = (value: unknown): value is Json =>
  value !== null && typeof value === 'object' && !Array.isArray(value);
const text = (value: unknown) => (typeof value === 'string' ? value : null);
const protocol = () => ResultExtensions.fail(ApiFailureFactory.create('protocol'));

function decodePerson(value: unknown): ConsolePerson | null {
  if (!isObject(value) || typeof value['sub'] !== 'string') return null;
  return { sub: value['sub'], name: text(value['name']), email: text(value['email']), picture: text(value['picture']) };
}

function decodeOrganization(value: unknown): ConsoleOrganization | null {
  if (!isObject(value) || typeof value['id'] !== 'string') return null;
  return { id: value['id'], name: text(value['name']), slug: text(value['slug']), role: text(value['role']) };
}

/** Reads the BFF's session answer: `{ isAuthenticated: false }`, or the person, organization, permissions and token. */
export function decodeSession(value: unknown): Result<ConsoleSession, ApiFailure> {
  if (!isObject(value) || typeof value['isAuthenticated'] !== 'boolean') return protocol();
  if (!value['isAuthenticated']) return ResultExtensions.ok({ isAuthenticated: false });
  const person = decodePerson(value['person']);
  const permissions = value['permissions'];
  if (
    !person ||
    typeof value['csrfToken'] !== 'string' ||
    !Array.isArray(permissions) ||
    !permissions.every((item) => typeof item === 'string')
  )
    return protocol();
  return ResultExtensions.ok({
    isAuthenticated: true,
    person,
    organization: decodeOrganization(value['organization']),
    permissions,
    csrfToken: value['csrfToken'],
    expiresAt: text(value['expiresAt']) ?? '',
  });
}

/** Calls Identity's BFF session routes on this origin, with the browser's session cookie. */
export const sessionApi = {
  getSession(signal?: AbortSignal) {
    return client.get<ConsoleSession>(SessionRouteConstants.session, {
      decode: decodeSession,
      ...(signal ? { signal } : {}),
    });
  },
  /** Ends the local session; answers the identity instance's end-session URL to send the browser to. */
  signOut(csrfToken: string) {
    return client.post<string>(SessionRouteConstants.signOut, {
      headers: { [SessionRouteConstants.csrfHeader]: csrfToken },
      decode: (value) =>
        isObject(value) && typeof value['endSessionUrl'] === 'string'
          ? ResultExtensions.ok(value['endSessionUrl'])
          : protocol(),
    });
  },
} as const;
