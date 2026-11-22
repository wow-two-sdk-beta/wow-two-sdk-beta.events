/**
 * The neutral shape of the feed's filters: what `useEventsFilters` hands a page's own filter bar. They carry no rendering
 * (no icons, no component props), so any bar can read them; the fe sdk ships input primitives only, and each product
 * builds its bar from them.
 */

/** Defines how a facet takes its value: one, several, or on-off. */
export const EventsFacetKind = {
  /** Refers to at most one chosen value; choosing the chosen one again clears it. */
  Single: 'single',
  /** Refers to any number of chosen values. */
  Multi: 'multi',
  /** Refers to one on-or-off value, `true` when on and no key when off; it has no options. */
  Toggle: 'toggle',
} as const;
export type EventsFacetKind = (typeof EventsFacetKind)[keyof typeof EventsFacetKind];

/** Defines where a bar offers a facet. */
export const EventsFacetPlacement = {
  /** Refers to a row of chips beside the search, one click from every value. */
  Inline: 'inline',
  /** Refers to a picker or switch row behind the bar's "Filters" button, its chosen values shown as removable chips. */
  Popover: 'popover',
} as const;
export type EventsFacetPlacement = (typeof EventsFacetPlacement)[keyof typeof EventsFacetPlacement];

/** Defines one choice of a facet. */
export interface EventsFacetOption {
  /** The value stored in the selection. */
  readonly value: string;
  /** The text on the chip, in the picker and on the removable chip. */
  readonly label: string;
  /** How many rows this value would match; absent without the server's counts (outside `tracking`, or not read yet). */
  readonly count?: number;
}

/** Defines what every facet carries, whatever its kind. */
interface EventsFacetBase {
  /** The facet's key in the selection. */
  readonly key: string;
  /** The facet's name: the group's accessible name, the picker's heading and the prefix of its chips. */
  readonly label: string;
  /** Whether the facet sits in the bar or behind its "Filters" button. */
  readonly placement: EventsFacetPlacement;
}

/** Defines a facet that offers choices: a named dimension rows are filtered by, with the values the reader sees. */
export interface EventsChoiceFacet extends EventsFacetBase {
  /** Whether the facet takes one value or several. */
  readonly kind: Exclude<EventsFacetKind, 'toggle'>;
  /** The choices, in display order. */
  readonly options: readonly EventsFacetOption[];
}

/** Defines a facet with one on-or-off value ("High priority", "Show skipped") and no options. */
export interface EventsToggleFacet extends EventsFacetBase {
  /** Marks the facet as an on-off switch; its selection value is `true` while on, and the key is absent while off. */
  readonly kind: 'toggle';
  /** How many rows turning the toggle on would match; absent without the server's counts. */
  readonly count?: number;
}

/** Defines one facet: a choice facet (`single`, `multi`) or a `toggle`, told apart by `kind`. */
export type EventsFacet = EventsChoiceFacet | EventsToggleFacet;

/**
 * The chosen values by facet key: a string for a `single` facet, an array for a `multi` one, `true` for a `toggle`.
 * A facet with nothing chosen has no key.
 */
export type EventsFilterSelection = Readonly<Record<string, string | readonly string[] | boolean | null | undefined>>;
