/** Mirrors `DatePrecision`: how much of a date is known; a value never claims more than its precision. */
export const DatePrecisionValues = ['unknown', 'year', 'month', 'day', 'minute'] as const;
export type DatePrecision = (typeof DatePrecisionValues)[number];

/** Mirrors `DateCertainty`: how sure a date is. */
export const DateCertaintyValues = ['announced', 'confirmed', 'estimated'] as const;
export type DateCertainty = (typeof DateCertaintyValues)[number];

/**
 * Mirrors `EventWhen`: an honest date. Its value carries exactly its precision — `2026`, `2026-03`, `2026-03-14` or
 * `2026-03-14T19:00` (local time, no offset) — and is absent when the precision is `unknown`.
 */
export interface EventWhen {
  readonly value?: string | null;
  readonly precision: DatePrecision;
  readonly certainty: DateCertainty;
}
