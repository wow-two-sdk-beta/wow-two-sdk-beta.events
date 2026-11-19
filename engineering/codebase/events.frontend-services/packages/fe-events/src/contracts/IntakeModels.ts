/** Mirrors `IntakeSourceStatusModel`: one SCFD source the service pulls. */
export interface IntakeSourceStatusModel {
  readonly name: string;
  readonly catalog: string;
  readonly subscriptionId: string;
  readonly enabled: boolean;
  readonly cursor?: string | null;
  readonly lastPolledAtUtc?: string | null;
  readonly lastReceivedAtUtc?: string | null;
  readonly newestObservedAtUtc?: string | null;
  readonly lastError?: string | null;
  readonly consecutiveFailures?: number;
  readonly received?: number;
  readonly quarantined?: number;
}

/** Mirrors `IntakeStatusModel`: every source plus what waits for a reviewer. */
export interface IntakeStatusModel {
  readonly sources: readonly IntakeSourceStatusModel[];
  readonly candidates: number;
  readonly pendingChanges: number;
  readonly oldestUnresolvedAtUtc?: string | null;
}

/**
 * Mirrors `IntakeFreshnessModel`: how fresh a catalog's intake is, for the catalog's members. Times and a count only —
 * never a cursor, an error text or an address. Both times are absent before the catalog's first record.
 */
export interface IntakeFreshnessModel {
  /** When a source of the catalog last committed a page with records. */
  readonly lastReceivedAtUtc?: string | null;
  /** The newest observed time among the records received; now minus it is the consumer's lag. */
  readonly newestObservedAtUtc?: string | null;
  /** How many of the catalog's polling sources failed their last poll. */
  readonly failingSources: number;
}
