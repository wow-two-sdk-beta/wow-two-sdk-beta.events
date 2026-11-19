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
          stages: [],
          excludeStages: [],
        },
        linkTemplate: null,
        expectedRevision: null,
      },
    });
  });

  it('reads and writes the stages a digest includes and leaves out', () => {
    const draft = digestDraft({
      ...defaultDigestSpec,
      selection: { deadlineWithinDays: 7, stages: ['shortlisted', 'applying'], excludeStages: ['skipped'] },
    });

    expect(draft.stages).toEqual(['shortlisted', 'applying']);
    expect(draft.excludeStages).toEqual(['skipped']);
    expect(digestSpec(draft, 2)).toMatchObject({
      spec: {
        selection: { deadlineWithinDays: 7, stages: ['shortlisted', 'applying'], excludeStages: ['skipped'] },
        expectedRevision: 2,
      },
    });
  });

  it('names a schedule without a day', () => {
    const draft = { ...digestDraft(defaultDigestSpec), days: [] };
    expect(digestSpec(draft, 1)).toEqual({ problem: { key: 'digestDaysRequired' } });
  });

  it('writes an instant in the schedule’s zone, with named months and weekdays per language', () => {
    const next = '2026-11-23T04:00:00Z';

    expect(formatInZone(next, 'Asia/Tashkent', 'en')).toBe('Mon, Nov 23, 2026, 09:00 (Asia/Tashkent)');
    expect(formatInZone(next, 'Asia/Tashkent', 'uz')).toBe('23-noyabr 2026, dushanba 09:00 (Asia/Tashkent)');
    expect(formatInZone(next, 'Asia/Tashkent', 'ru')).toBe('23 ноября 2026, понедельник 09:00 (Asia/Tashkent)');
  });

  it('reads the zone’s own day, not the viewer’s or UTC’s', () => {
    // 20:30 UTC on Saturday is 01:30 on Sunday in Tashkent and 12:30 the same Saturday in Los Angeles.
    const instant = '2026-12-05T20:30:00Z';

    expect(formatInZone(instant, 'Asia/Tashkent', 'uz')).toBe('6-dekabr 2026, yakshanba 01:30 (Asia/Tashkent)');
    expect(formatInZone(instant, 'Asia/Tashkent', 'ru')).toBe('6 декабря 2026, воскресенье 01:30 (Asia/Tashkent)');
    expect(formatInZone(instant, 'America/Los_Angeles', 'en')).toBe('Sat, Dec 5, 2026, 12:30 (America/Los_Angeles)');
  });

  it('writes midnight as 00:00 and names every month and weekday', () => {
    expect(formatInZone('2026-12-31T19:00:00Z', 'Asia/Tashkent', 'uz')).toBe(
      '1-yanvar 2027, juma 00:00 (Asia/Tashkent)',
    );

    const months = Array.from({ length: 12 }, (_, month) =>
      formatInZone(new Date(Date.UTC(2026, month, 10, 12)).toISOString(), 'UTC', 'uz'),
    );
    expect(months.map((text) => text.split(' ')[0])).toEqual([
      '10-yanvar',
      '10-fevral',
      '10-mart',
      '10-aprel',
      '10-may',
      '10-iyun',
      '10-iyul',
      '10-avgust',
      '10-sentabr',
      '10-oktabr',
      '10-noyabr',
      '10-dekabr',
    ]);
    const weekdays = Array.from(
      { length: 7 },
      (_, day) =>
        formatInZone(new Date(Date.UTC(2026, 10, 22 + day, 12)).toISOString(), 'UTC', 'uz')
          .split(', ')[1]!
          .split(' ')[0],
    );
    expect(weekdays).toEqual(['yakshanba', 'dushanba', 'seshanba', 'chorshanba', 'payshanba', 'juma', 'shanba']);
  });

  it('returns an unreadable instant or zone as it was', () => {
    expect(formatInZone('soon', 'Asia/Tashkent', 'uz')).toBe('soon');
    expect(formatInZone('2026-11-23T04:00:00Z', 'Mars/Olympus', 'uz')).toBe('2026-11-23T04:00:00Z');
  });
});
