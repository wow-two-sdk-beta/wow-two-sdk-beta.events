import type { TrackingSpec } from '../../contracts';

/** Shapes a feed's "Propose an event" form: what it offers and the tracking it writes with the new event. */
export interface EventProposalOptions {
  /** The tracking written right after the event is created (Ocharo: shortlisted, with its cost and participation). */
  readonly initialTracking?: TrackingSpec;
  /** The tracking field "Where it was found" is also stored in, with `initialTracking` (Ocharo: `sourceUrl`). */
  readonly sourceField?: string;
  /** Whether the application deadline is offered. */
  readonly withDeadline?: boolean;
  /** Whether "Where it was found" is offered. */
  readonly withFoundAt?: boolean;
  /** Whether the catalog is private, so the event publishes at once: the button reads "Add event". */
  readonly privateCatalog?: boolean;
}
