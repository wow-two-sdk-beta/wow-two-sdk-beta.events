import type { EventModel, TrackingModel } from '../../contracts';

/** Holds what every `EventMediaCard` slot receives: the event and the organization's tracking of it, when it has any. */
export interface EventCardSlotBase {
  readonly event: EventModel;
  readonly tracking: TrackingModel | null;
}

/** `media`: the zone behind the card. The curated image is the default; with no image the `fallback` zone shows. */
export interface EventCardMediaSlot extends EventCardSlotBase {
  /** The curated image the card would show (`facts.images[0]`, http or https only); null with none or after a failed load. */
  readonly image: { readonly url: string; readonly alt: string } | null;
}

/** `fallback`: the tile shown when there is no media. The date tile is the default. */
export interface EventCardFallbackSlot extends EventCardSlotBase {
  /** The start as a tile: the day and month, or the month and year for a month start; null when undated or a year only. */
  readonly tile: { readonly big: string; readonly small: string; readonly month: string } | null;
  /** The event's kind key, for an icon. */
  readonly kindKey: string | undefined;
}

/** `pills` (top start): the kind, stage, status, online and removed pills are the default. Labels are not lower-cased. */
export interface EventCardPillsSlot extends EventCardSlotBase {
  /** The kind's label; empty without a kind. */
  readonly kind: string;
  /** The stage's label; empty while the event is untracked. */
  readonly stage: string;
  /** "Cancelled" or "postponed" when the facts say so, else empty. */
  readonly status: string;
  readonly online: boolean;
  /** Whether every source listing is gone. */
  readonly removed: boolean;
}

/** `actions` (top end): the high-priority mark and the quick-stage menu are the default. */
export interface EventCardActionsSlot extends EventCardSlotBase {
  readonly highPriority: boolean;
  /** The quick stages the menu offers: only for an event at the default stage that the person may track. */
  readonly quickStages: readonly string[];
  /** Whether a quick stage's write is running. */
  readonly pending: boolean;
  /** Asks the owner to move the event to a stage; the card emits `track`. */
  track(stage: string): void;
}

/** `title`: the content of the card's title heading; the stretched link (or the `open` button) is the default. */
export interface EventCardTitleSlot extends EventCardSlotBase {
  readonly title: string;
  /** The event's own page, when the owner built one. */
  readonly href: string | undefined;
  /** Opens the event: the card emits `open`. */
  open(): void;
  /** A click handler for a link to `href`: a plain click emits `open`, a modified one follows the link. */
  follow(event: MouseEvent): void;
}

/** `meta`: the dates · city line; the dates, an ongoing or finished mark and the city are the default. */
export interface EventCardMetaSlot extends EventCardSlotBase {
  readonly dates: string;
  readonly city: string;
  readonly finished: boolean;
  readonly ongoing: boolean;
}

/** `detail`: the line under the meta; the next open deadline, else the source names, are the default. */
export interface EventCardDetailSlot extends EventCardSlotBase {
  /** The next open deadline, as the card words it: `apply by 3 Mar`, days left (`5d`) and its tone. */
  readonly deadline: {
    readonly label: string;
    readonly left: string;
    readonly tone: 'urgent' | 'upcoming' | 'passed';
  } | null;
  /** The sources' names without repeats, as stored. */
  readonly sources: readonly string[];
}

/** Names the zones of an `EventMediaCard` that take a slot; `EventsFeed` forwards each as `card-{zone}`. */
export const EventCardZones = ['media', 'fallback', 'pills', 'actions', 'title', 'meta', 'detail'] as const;
export type EventCardZone = (typeof EventCardZones)[number];
