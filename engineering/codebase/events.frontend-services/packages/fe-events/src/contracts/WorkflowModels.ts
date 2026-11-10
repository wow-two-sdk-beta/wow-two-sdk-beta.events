import type { JsonValue, LocalizedText } from './Common';

/** Mirrors `StageClass`: what a stage means across organizations. */
export const StageClassValues = ['open', 'active', 'won', 'lost'] as const;
export type StageClass = (typeof StageClassValues)[number];

/** Mirrors `WorkflowStageModel`. */
export interface WorkflowStageModel {
  readonly key: string;
  readonly label?: LocalizedText;
  readonly class?: StageClass;
}

/** Mirrors `WorkflowModel`: one organization's stages and custom fields; revision 0 for the default never written. */
export interface WorkflowModel {
  readonly stages: readonly WorkflowStageModel[];
  readonly defaultStage: string;
  /** The custom fields' JSON Schema; stored, not enforced in P1. */
  readonly fields?: JsonValue;
  readonly revision: number;
}

/** Mirrors `WorkflowSpec`. */
export interface WorkflowSpec {
  readonly stages: readonly WorkflowStageModel[];
  readonly defaultStage: string;
  readonly fields?: JsonValue;
  readonly expectedRevision?: number | null;
}
