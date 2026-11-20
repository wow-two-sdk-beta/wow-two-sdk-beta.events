/**
 * How the feed lays out the fe sdk's `FacetBar` on a phone, written against the `data-facet-*` markers the bar puts on
 * its parts, so it holds for the bar's versions (one scrolling chip row, or chips that wrap):
 * - the scope chips and the kind chips each sit on one row that scrolls sideways, instead of wrapping into rows of
 *   their own (eight kinds were four rows, 650 px of chips before the first card);
 * - the end controls take the whole width and start at its left, so a row never ends short on the right while the
 *   next one starts there: the controls that may grow (the sort, "Propose an event") fill what is left of their row.
 * `sm` and up keep the bar's own layout. Every class is written whole, for the consumer's Tailwind scan of `dist`.
 */
export const feedFacetBarClass = [
  // The chip groups: a column of full-width rows, with no padding or pull of their own.
  'max-sm:[&_[data-facet-inline]]:m-0',
  'max-sm:[&_[data-facet-inline]]:w-full',
  'max-sm:[&_[data-facet-inline]]:basis-full',
  'max-sm:[&_[data-facet-inline]]:flex-col',
  'max-sm:[&_[data-facet-inline]]:flex-nowrap',
  'max-sm:[&_[data-facet-inline]]:items-stretch',
  'max-sm:[&_[data-facet-inline]]:gap-y-2',
  'max-sm:[&_[data-facet-inline]]:overflow-visible',
  'max-sm:[&_[data-facet-inline]]:p-0',
  // Each group is one row that scrolls: its chips keep their width, and a little room keeps the focus ring whole.
  'max-sm:[&_[data-facet-inline]>*]:min-w-0',
  'max-sm:[&_[data-facet-inline]>*]:flex-nowrap',
  'max-sm:[&_[data-facet-inline]>*]:overflow-x-auto',
  'max-sm:[&_[data-facet-inline]>*]:-m-1',
  'max-sm:[&_[data-facet-inline]>*]:p-1',
  'max-sm:[&_[data-facet-inline]>*]:[scrollbar-width:none]',
  'max-sm:[&_[data-facet-inline]>*>*]:shrink-0',
  // The end controls: the whole width, from the left.
  'max-sm:[&_[data-facet-end]]:ms-0',
  'max-sm:[&_[data-facet-end]]:w-full',
  'max-sm:[&_[data-facet-end]]:justify-start',
  // "Clear all" is as tall as the other controls on the bar, at every width.
  '[&_[data-facet-clear-all]]:min-h-10',
].join(' ');

/** The sort select: as tall as the bar's search and Filters (40 px), pill-shaped like them. */
export const feedSortClass =
  'min-h-10 rounded-full border border-border bg-background px-3 py-1.5 text-sm text-foreground max-sm:w-full';

/** An attention pill ("{n} deadlines in 14 days", the consumer's own): 40 px high on a phone, 32 px from `sm`. */
export const feedPillClass = 'min-h-10 rounded-full border px-3 py-1 text-sm sm:min-h-8';
