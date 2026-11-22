/**
 * The sort trigger in the feed's actions row: pill-shaped, like the attention pills. The fe sdk's `SelectPickerTrigger`
 * brings the 40 px height (as tall as the row's other controls), the border and the field fill; this class sets the
 * shape only. Written whole, for the consumer's Tailwind scan of `dist`.
 */
export const feedSortClass = 'rounded-full';

/** An attention pill ("{n} deadlines in 14 days", the consumer's own): 40 px high on a phone, 32 px from `sm`. */
export const feedPillClass = 'min-h-10 rounded-full border px-3 py-1 text-sm sm:min-h-8';
