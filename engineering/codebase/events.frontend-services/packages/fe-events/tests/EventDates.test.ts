import { describe, expect, it } from 'vitest';
import type { EventWhen } from '../src/contracts';
import {
  daysLeftLabel,
  deadlineDaysLeft,
  eventGroupHeading,
  eventWhenRange,
  formatEventSpan,
  formatEventWhen,
  groupEventsByMonth,
  isEventWhenValue,
  sortEventsByDate,
} from '../src/kit';
import { eventModel } from './Fixtures';

const when = (
  value: string | null,
  precision: EventWhen['precision'],
  certainty: EventWhen['certainty'] = 'announced',
): EventWhen => ({
  value,
  precision,
  certainty,
});

describe('one date', () => {
  it('writes a month as the month, never a day', () => {
    expect(formatEventWhen(when('2026-10', 'month'), 'en')).toBe('Oct 2026');
    expect(formatEventWhen(when('2026-10', 'month'), 'ru')).toBe('окт. 2026');
    expect(formatEventWhen(when('2026-10', 'month'), 'uz')).toBe('okt 2026');
    expect(formatEventWhen(when('2026-10-12', 'month'), 'en')).toBe('Oct 2026');
  });

  it('writes a day, a time and a year as precisely as known', () => {
    expect(formatEventWhen(when('2026-10-12', 'day'), 'en')).toBe('12 Oct 2026');
    expect(formatEventWhen(when('2026-03-14T19:00', 'minute'), 'en')).toBe('14 Mar 2026, 19:00');
    expect(formatEventWhen(when('2026', 'year'), 'ru')).toBe('2026');
    expect(formatEventWhen(when('2026-03', 'day'), 'en')).toBe('Mar 2026');
  });

  it('marks an estimated date as expected', () => {
    expect(formatEventWhen(when('2026-10', 'month', 'estimated'), 'en')).toBe('≈ Oct 2026 (expected)');
    expect(formatEventWhen(when('2026-10', 'month', 'estimated'), 'ru')).toBe('≈ окт. 2026 (ожидается)');
    expect(formatEventWhen(when('2026-10', 'month', 'estimated'), 'uz')).toBe('≈ okt 2026 (kutilmoqda)');
  });

  it('says an unknown or unreadable date is not announced', () => {
    expect(formatEventWhen(when(null, 'unknown'), 'en')).toBe('Date not announced');
    expect(formatEventWhen(when('2026-13', 'month'), 'en')).toBe('Date not announced');
    expect(formatEventWhen(when('2026-02-30', 'day'), 'en')).toBe('Date not announced');
    expect(formatEventSpan(when(null, 'unknown'), null, 'en')).toBe('Dates not announced');
    expect(formatEventSpan(undefined, undefined, 'uz')).toBe('Sanalar e’lon qilinmagan');
    expect(formatEventSpan(when(null, 'unknown'), null, 'ru')).toBe('Даты не объявлены');
  });
});

describe('spans', () => {
  it('shares the month and year of days in one month', () => {
    expect(formatEventSpan(when('2026-10-12', 'day'), when('2026-10-14', 'day'), 'en')).toBe('12–14 Oct 2026');
  });

  it('writes both months of days across months, and both years across years', () => {
    expect(formatEventSpan(when('2026-10-28', 'day'), when('2026-11-02', 'day'), 'en')).toBe('28 Oct – 2 Nov 2026');
    expect(formatEventSpan(when('2026-10-28', 'day'), when('2026-11-02', 'day'), 'ru')).toBe('28 окт. – 2 нояб. 2026');
    expect(formatEventSpan(when('2026-12-30', 'day'), when('2027-01-02', 'day'), 'en')).toBe(
      '30 Dec 2026 – 2 Jan 2027',
    );
  });

  it('spans months without inventing days', () => {
    expect(formatEventSpan(when('2026-10', 'month'), when('2026-11', 'month'), 'en')).toBe('Oct – Nov 2026');
    expect(formatEventSpan(when('2026-12', 'month'), when('2027-01', 'month'), 'en')).toBe('Dec 2026 – Jan 2027');
    expect(formatEventSpan(when('2026-10', 'month', 'estimated'), when('2026-11', 'month'), 'en')).toBe(
      '≈ Oct – Nov 2026 (expected)',
    );
  });

  it('writes each end at its own precision when they differ', () => {
    expect(formatEventSpan(when('2026-10-28', 'day'), when('2026-11', 'month'), 'en')).toBe('28 Oct 2026 – Nov 2026');
  });

  it('ignores an end before the start, or equal to it', () => {
    expect(formatEventSpan(when('2026-10-12', 'day'), when('2026-10-01', 'day'), 'en')).toBe('12 Oct 2026');
    expect(formatEventSpan(when('2026-10-12', 'day'), when('2026-10-12', 'day'), 'en')).toBe('12 Oct 2026');
    expect(formatEventSpan(when('2026-03-14T19:00', 'minute'), when('2026-03-14T22:00', 'minute'), 'en')).toBe(
      '14 Mar 2026, 19:00–22:00',
    );
  });
});

describe('ranges, order and groups', () => {
  it('covers a whole month or year in a range', () => {
    expect(eventWhenRange(when('2026-02', 'month'))).toEqual({ first: '2026-02-01', last: '2026-02-28' });
    expect(eventWhenRange(when('2026', 'year'))).toEqual({ first: '2026-01-01', last: '2026-12-31' });
    expect(eventWhenRange(when(null, 'unknown'))).toBeNull();
  });

  it('checks a value against its precision', () => {
    expect(isEventWhenValue('2026-03', 'month')).toBe(true);
    expect(isEventWhenValue('2026-03-14', 'month')).toBe(false);
    expect(isEventWhenValue('2026-03-14T19:00', 'minute')).toBe(true);
    expect(isEventWhenValue('14.03.2026', 'day')).toBe(false);
  });

  it('sorts by start and closes with undated events', () => {
    const events = [
      eventModel('undated'),
      eventModel('october', { starts: when('2026-10', 'month') }),
      eventModel('march', { starts: when('2026-03-14', 'day') }),
    ];
    expect(sortEventsByDate(events).map((event) => event.id)).toEqual(['march', 'october', 'undated']);
  });

  it('groups by month, a year-only event after its year, undated last', () => {
    const groups = groupEventsByMonth([
      eventModel('undated'),
      eventModel('next-year', { starts: when('2027-01-10', 'day') }),
      eventModel('year', { starts: when('2026', 'year') }),
      eventModel('october', { starts: when('2026-10', 'month', 'estimated') }),
      eventModel('october-day', { starts: when('2026-10-03', 'day') }),
      eventModel('march', { starts: when('2026-03-14', 'day') }),
    ]);
    expect(groups.map((group) => group.key)).toEqual(['2026-03', '2026-10', '2026', '2027-01', 'undated']);
    expect(groups[1]?.events.map((event) => event.id)).toEqual(['october', 'october-day']);
    expect(eventGroupHeading('2026-10', 'ru')).toBe('Октябрь 2026');
    expect(eventGroupHeading('2026', 'en')).toBe('2026, month not announced');
    expect(eventGroupHeading('undated', 'uz')).toBe('Sana e’lon qilinmagan');
  });

  it('counts days only to a deadline known to its day', () => {
    expect(deadlineDaysLeft({ at: when('2026-11-20', 'day') }, '2026-11-10')).toBe(10);
    expect(deadlineDaysLeft({ at: when('2026-11', 'month') }, '2026-11-10')).toBeNull();
    expect(daysLeftLabel(1, 'en')).toBe('1 day left');
    expect(daysLeftLabel(0, 'ru')).toBe('Срок сегодня');
    expect(daysLeftLabel(-2, 'uz')).toBe('Muddat o‘tgan');
  });
});
