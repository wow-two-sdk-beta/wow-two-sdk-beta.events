/** Names the date presets the feed's "Filters" popover offers; `upcoming` is the default and `custom` takes a range. */
export const EventDatePresetValues = ['upcoming', 'thisMonth', 'next3Months', 'thisYear', 'past', 'custom'] as const;
export type EventDatePreset = (typeof EventDatePresetValues)[number];

/** Holds the dates a preset selects (`YYYY-MM-DD`) and whether events with no date still list. */
export interface EventDateRange {
  readonly from?: string;
  readonly to?: string;
  /** True only for `upcoming`: an undated event is still to come, but belongs to no month, year or past. */
  readonly includeUndated: boolean;
}

const pad = (value: number) => String(value).padStart(2, '0');
const dayMs = 86_400_000;
const utc = (day: string) => Date.UTC(Number(day.slice(0, 4)), Number(day.slice(5, 7)) - 1, Number(day.slice(8, 10)));
const iso = (ms: number) => {
  const date = new Date(ms);
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
};

/** Adds whole calendar days to a `YYYY-MM-DD` day. */
export function addEventDays(day: string, days: number): string {
  return iso(utc(day) + days * dayMs);
}

/** Adds calendar months to a day, clamping to the last day of a shorter month (31 Jan + 1 month = 28 Feb). */
function addMonths(day: string, months: number): string {
  const total = Number(day.slice(0, 4)) * 12 + Number(day.slice(5, 7)) - 1 + months;
  const year = Math.floor(total / 12);
  const month = total % 12;
  const last = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  return `${year}-${pad(month + 1)}-${pad(Math.min(Number(day.slice(8, 10)), last))}`;
}

/**
 * Turns a preset into the `from` and `to` days the lists take, counted from `today`: `upcoming` from today on (undated
 * events included), `thisMonth` and `thisYear` the whole calendar month or year, `next3Months` today to three months
 * ahead, `past` up to yesterday. `custom` takes the range the reader picked and, without one yet, reads as `upcoming`.
 */
export function eventDateRange(
  preset: EventDatePreset,
  today: string,
  custom?: { readonly from?: string; readonly to?: string } | null,
): EventDateRange {
  const year = today.slice(0, 4);
  const month = today.slice(0, 7);
  switch (preset) {
    case 'thisMonth':
      return {
        from: `${month}-01`,
        to: iso(Date.UTC(Number(year), Number(today.slice(5, 7)), 0)),
        includeUndated: false,
      };
    case 'next3Months':
      return { from: today, to: addMonths(today, 3), includeUndated: false };
    case 'thisYear':
      return { from: `${year}-01-01`, to: `${year}-12-31`, includeUndated: false };
    case 'past':
      return { to: addEventDays(today, -1), includeUndated: false };
    case 'custom':
      return custom?.from || custom?.to
        ? {
            ...(custom.from ? { from: custom.from } : {}),
            ...(custom.to ? { to: custom.to } : {}),
            includeUndated: false,
          }
        : { from: today, includeUndated: true };
    default:
      return { from: today, includeUndated: true };
  }
}
