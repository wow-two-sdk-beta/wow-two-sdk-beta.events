import type { LocalizedText, TrackingQuery } from '../../contracts';

/**
 * Names a set of countries a consumer groups events by (Ocharo: Local `UZ`, Regional, International): a region lists its
 * ISO-2 `countries`, or takes every country no other region lists (`otherCountries`). An event with no country belongs
 * to no region.
 */
export interface EventRegion {
  readonly key: string;
  readonly label: LocalizedText;
  readonly countries?: readonly string[];
  readonly otherCountries?: boolean;
}

const code = (value: string | null | undefined) => {
  const text = value?.trim().toUpperCase() ?? '';
  return /^[A-Z]{2}$/.test(text) ? text : null;
};

/** Returns the region a country belongs to: the first that lists it, else the first `otherCountries` one; null without a country. */
export function regionOfCountry(
  country: string | null | undefined,
  regions: readonly EventRegion[] | undefined,
): EventRegion | null {
  const known = code(country);
  if (!known || !regions?.length) return null;
  return (
    regions.find((region) => region.countries?.some((item) => item.toUpperCase() === known)) ??
    regions.find((region) => region.otherCountries) ??
    null
  );
}

/**
 * Builds the tracking filter a region selects (E21): its `countries`, or for an `otherCountries` region every country the
 * other regions list as `excludeCountries`; an event with no country matches neither.
 */
export function regionQuery(
  region: EventRegion | null | undefined,
  regions: readonly EventRegion[],
): Pick<TrackingQuery, 'countries' | 'excludeCountries' | 'includeUnknownCountry'> {
  if (!region) return {};
  if (region.otherCountries) {
    const listed = regions.flatMap((item) => (item.otherCountries ? [] : (item.countries ?? [])));
    return { excludeCountries: [...new Set(listed.map((item) => item.toUpperCase()))], includeUnknownCountry: false };
  }
  return { countries: [...new Set((region.countries ?? []).map((item) => item.toUpperCase()))] };
}
