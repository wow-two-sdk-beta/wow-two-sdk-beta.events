import { describe, expect, it } from 'vitest';
import { defaultDigestSpec, digestDraft, digestSpec, formatInZone, parseRecipients } from '../src/admin';

describe('digest draft', () => {
  it('reads recipient lines: a sub or a role, then an optional language', () => {
    expect(parseRecipients('sub-1\nrole:member ru\n\n sub-2 uz-Latn ')).toEqual({
      list: [{ subject: 'sub-1' }, { role: 'member', language: 'ru' }, { subject: 'sub-2', language: 'uz-Latn' }],
    });
    expect(parseRecipients('sub-1 de')).toEqual({ line: 1 });
    expect(parseRecipients('ok\nrole:')).toEqual({ line: 2 });
  });

  it('writes the defaults back as the same spec', () => {
    const written = digestSpec(digestDraft({ ...defaultDigestSpec, recipients: [{ subject: 'sub-1' }] }), null);

    expect(written).toEqual({
      spec: {
        ...defaultDigestSpec,
        recipients: [{ subject: 'sub-1' }],
        selection: {
          newSinceLastRun: true,
          startingWithinDays: 30,
          deadlineWithinDays: 14,
          statuses: [],
          kinds: [],
          cities: [],
          trackedOnly: false,
        },
        linkTemplate: null,
        expectedRevision: null,
      },
    });
  });

  it('names a schedule without a day', () => {
    const draft = { ...digestDraft(defaultDigestSpec), days: [] };
    expect(digestSpec(draft, 1)).toEqual({ problem: { key: 'digestDaysRequired' } });
  });

  it('writes an instant in the schedule’s zone', () => {
    expect(formatInZone('2026-11-23T04:00:00Z', 'Asia/Tashkent', 'en')).toBe(
      'Mon, Nov 23, 2026, 09:00 (Asia/Tashkent)',
    );
  });
});
