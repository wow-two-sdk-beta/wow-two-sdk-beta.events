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
