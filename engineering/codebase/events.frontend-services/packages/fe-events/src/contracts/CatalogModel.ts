import type { LocalizedText } from './Common';

/** Mirrors `CatalogVisibility`. */
export const CatalogVisibilityValues = ['private', 'shared', 'public'] as const;
export type CatalogVisibility = (typeof CatalogVisibilityValues)[number];

/** Mirrors `CatalogKindModel`: one kind of event a catalog accepts. */
export interface CatalogKindModel {
  readonly key: string;
  readonly label?: LocalizedText;
}

/** Mirrors `CatalogModel`: a curated namespace of events. */
export interface CatalogModel {
  readonly slug: string;
  readonly name: string;
  readonly ownerIssuer: string;
  readonly ownerOrganizationId: string;
  readonly visibility: CatalogVisibility;
  readonly locales?: readonly string[];
  readonly kinds?: readonly CatalogKindModel[];
  readonly revision: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

/** Mirrors `CatalogSpec`. */
export interface CatalogSpec {
  readonly name: string;
  readonly visibility?: CatalogVisibility;
  readonly locales?: readonly string[];
  readonly kinds?: readonly CatalogKindModel[];
  readonly expectedRevision?: number | null;
}
