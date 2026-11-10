import { describe, expect, it } from 'vitest';
import { hasPermission, signInUrl } from '../src/domain/session/ConsoleSession';
import { decodeSession } from '../src/integration/session';

describe('session', () => {
  it('reads a signed-out answer', () => {
    expect(decodeSession({ isAuthenticated: false })).toEqual({ ok: true, value: { isAuthenticated: false } });
  });

  it('reads a live session with its organization and permissions', () => {
    const result = decodeSession({
      isAuthenticated: true,
      person: { sub: 'p1', name: null, email: 'malika@ocharo.test' },
      organization: { id: 'o1', name: 'Ocharo', role: 'owner' },
      permissions: ['events:review'],
      csrfToken: 'csrf-1',
      expiresAt: '2026-11-11T00:00:00Z',
    });

    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(hasPermission(result.value, 'events:review')).toBe(true);
      expect(hasPermission(result.value, 'events:admin')).toBe(false);
    }
  });

  it.each([null, {}, { isAuthenticated: true, person: { sub: 'p1' } }])('refuses %j', (value) => {
    expect(decodeSession(value).ok).toBe(false);
  });

  it('returns to a path on this origin only', () => {
    expect(signInUrl('/events?catalog=ocharo')).toBe('/api/identity/sign-in?returnUrl=%2Fevents%3Fcatalog%3Docharo');
    expect(signInUrl('//evil.test/')).toBe('/api/identity/sign-in?returnUrl=%2F');
  });
});
