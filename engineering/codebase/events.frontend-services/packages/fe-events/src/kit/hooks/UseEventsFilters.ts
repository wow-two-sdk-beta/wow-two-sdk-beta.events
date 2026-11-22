import { RegionCodes, regionFlag, regionName } from '@wow-two-beta/fe/foundation/i18n';
import {
  FacetKind,
  FacetPlacement,
  type FacetDefinition,
  type FacetOption,
  type FacetSelection,
} from '@wow-two-beta/fe/presentation/forms';
import {
  computed,
  getCurrentInstance,
  onMounted,
  onScopeDispose,
  shallowRef,
  toValue,
  watch,
  type ComputedRef,
  type MaybeRefOrGetter,
  type Ref,
} from 'vue';
import {
  EventsConstants,
  TrackingSortValues,
  type CatalogKindModel,
  type EventQuery,
  type TrackingFacetCount,
  type TrackingQuery,
  type TrackingSort,
  type WorkflowModel,
} from '../../contracts';
import type { EventsClient } from '../client/CreateEventsClient';
import { localIsoDate } from '../dates/EventDates';
import { addEventDays, eventDateRange, EventDatePresetValues, type EventDatePreset } from '../facts/EventDatePresets';
import { regionQuery, type EventRegion } from '../facts/EventRegions';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../text/EventsMessages';
import { useEventsRequest } from './UseEventsRequest';

/** Holds what a page passes: each option is a value, a ref or a getter, and the state follows the ones that change. */
export interface EventsFiltersOptions {
  readonly client: MaybeRefOrGetter<EventsClient>;
  readonly catalog: MaybeRefOrGetter<string>;
  readonly locale?: MaybeRefOrGetter<EventsLocale | undefined>;
  /** The person's calendar day, `YYYY-MM-DD`; the browser's by default. */
  readonly today?: MaybeRefOrGetter<string | undefined>;
  /** The catalog's kinds, for the filter and the labels. */
  readonly kinds?: MaybeRefOrGetter<readonly CatalogKindModel[] | undefined>;
  /** Whether the state is the organization's tracking (stages, priority, server counts) instead of the catalog's events. */
  readonly tracking?: MaybeRefOrGetter<boolean | undefined>;
  /** The organization's workflow; read with `client.getWorkflow()` when absent and `tracking` is on. */
  readonly workflow?: MaybeRefOrGetter<Pick<WorkflowModel, 'stages' | 'defaultStage'> | null | undefined>;
  /** Stages left out unless their own filter or the "Show …" toggle asks. */
  readonly hiddenStages?: MaybeRefOrGetter<readonly string[] | undefined>;
  /** The consumer's regions: scope chips over country sets (with counts, summed from the countries facet). */
  readonly regions?: MaybeRefOrGetter<readonly EventRegion[] | undefined>;
}

/** Holds the range a reader picked for the `custom` date preset, as `YYYY-MM-DD` days. */
export interface EventsCustomRange {
  readonly from: string;
  readonly to: string;
}

/**
 * The URL query params a state travels in: `q`, `stages`, `kinds`, `cities`, `sources`, `regions` (the scope's key),
 * `when` and `sort`. A list is an array, which a router writes as a repeated key.
 */
export type EventsRouteQuery = Readonly<Record<string, string | readonly (string | null)[] | null | undefined>>;

/** Holds the feed's filter state, so a page can host the filter bar in a card and an `EventsFeed` can list from it. */
export interface EventsFilters {
  /** The search box's text; bind it with `v-model:search`. The query takes it after a short pause. */
  readonly search: Ref<string>;
  /** The picks by facet key; bind it with `v-model`. A write keeps no default (the "All" scope, the upcoming dates). */
  readonly selection: Ref<FacetSelection>;
  /** How the tracking list is ordered; bind it with `v-model` on a select. */
  readonly sort: Ref<TrackingSort>;
  /** The range the `custom` preset lists; a preset other than `custom` clears it. */
  readonly customRange: Ref<EventsCustomRange | null>;
  /** The date preset the `when` facet holds, `upcoming` by default. */
  readonly preset: ComputedRef<EventDatePreset>;
  /** Whether the state is the organization's tracking. */
  readonly tracking: ComputedRef<boolean>;
  /** The workflow: the option's, else the one read from the client; null until it arrives. */
  readonly workflow: ComputedRef<Pick<WorkflowModel, 'stages' | 'defaultStage'> | null>;
  /** The facets for the fe sdk `FacetBar`, with the server's counts under `tracking`. */
  readonly facetDefinitions: ComputedRef<readonly FacetDefinition[]>;
  /** How many events the current query lists (tracking only); the bar's match pill. */
  readonly matchCount: ComputedRef<number | undefined>;
  /** How many events the default view lists (tracking only), once `matchCount` is known. */
  readonly totalCount: ComputedRef<number | undefined>;
  /** The catalog query the feed lists with, without `tracking`. */
  readonly eventQuery: ComputedRef<EventQuery>;
  /** The tracking query the feed lists with under `tracking`, before its sort, cursor and limit. */
  readonly trackingQuery: ComputedRef<TrackingQuery>;
  /** Changes whenever what the feed lists changes: the query, and under `tracking` the sort. */
  readonly queryKey: ComputedRef<string>;
  /** Whether the reader chose anything: a search or a pick. */
  readonly filtered: ComputedRef<boolean>;
  /** Stores what the bar reports, leaving the defaults out: the "All" scope and the upcoming dates are no choice. */
  select(selection: FacetSelection): void;
  /** Reads the facet counts again (tracking only), after a write that changes them. */
  refresh(): void;
  /**
   * Builds the tracking query for a window of days (`YYYY-MM-DD`): every picked filter, with `from` and `to` as given.
   * The date preset is ignored, and undated events stay out. An event kept by month or year (`precision: month`,
   * `year`) is listed when its span overlaps the window, and reaches the page with its precision unchanged.
   */
  toWindowQuery(from: string, to: string): TrackingQuery;
  /** Writes the state as URL query params, leaving out what holds no choice. */
  toRouteQuery(): Record<string, string | string[]>;
  /** Reads the state from URL query params; a page given the same query reads the same filters. */
  fromRouteQuery(query: EventsRouteQuery): void;
}

const allScopes = 'all';
const deadlineFacetDays = 30;
const debounceMs = 250;
/** The facet keys `toRouteQuery` carries; the other picks (toggles, a country list) stay where they are. */
const routeFacetKeys = ['stage', 'kind', 'city', 'source', 'scope', 'when'] as const;

const presetKeys: Record<EventDatePreset, EventsMessageKey> = {
  upcoming: 'datePresetUpcoming',
  today: 'datePresetToday',
  thisMonth: 'datePresetThisMonth',
  next3Months: 'datePresetNext3Months',
  thisYear: 'datePresetThisYear',
  past: 'datePresetPast',
  custom: 'datePresetCustom',
};

const upper = (value: string) => value.toUpperCase();
const countOf = (list: readonly TrackingFacetCount[] | undefined, value: string) =>
  list?.find((item) => item.value === value)?.count;

/** Writes a selection with its keys sorted, so two selections of the same picks compare equal. */
function canonical(selection: FacetSelection): string {
  return JSON.stringify(Object.entries(selection).sort(([a], [b]) => a.localeCompare(b)));
}

/**
 * Owns the filter state the events feed lists with: the search, the picks, the sort, the date preset and its custom range;
 * the facet definitions with the server's counts and the match and total counts under `tracking`; and the query that
 * state means. A page hosts the fe sdk `FacetBar` itself and hands the result to `EventsFeed` as `filters`, so the bar
 * and the list read one state. Like `useEventsRequest`, call it in a component's setup: its requests abort with the
 * component, and under `tracking` it reads the workflow and the counts once the component mounts.
 */
export function useEventsFilters(options: EventsFiltersOptions): EventsFilters {
  const client = () => toValue(options.client);
  const catalog = () => toValue(options.catalog);
  const locale = computed(() => toValue(options.locale) ?? 'en');
  const day = computed(() => toValue(options.today) ?? localIsoDate());
  const kinds = computed(() => toValue(options.kinds));
  const tracking = computed(() => toValue(options.tracking) ?? false);
  const regions = computed(() => toValue(options.regions));
  const hidden = computed(() => (toValue(options.hiddenStages) ?? []).filter((key) => key !== ''));

  const search = shallowRef('');
  const text = shallowRef('');
  const selected = shallowRef<FacetSelection>({});
  const sort = shallowRef<TrackingSort>('starts');
  const customRange = shallowRef<EventsCustomRange | null>(null);
  let timer: ReturnType<typeof setTimeout> | undefined;

  const workflowRequest = useEventsRequest((signal) => client().getWorkflow(signal));
  const workflow = computed(() => toValue(options.workflow) ?? workflowRequest.value.value ?? null);
  const stages = computed(() => workflow.value?.stages ?? []);
  const hiddenLabels = computed(() =>
    hidden.value.map((key) => EventsLabels.stage(key, stages.value, locale.value)).join(', '),
  );

  /** The values a choice facet holds, whatever its kind; a toggle holds none. */
  function pick(key: string): readonly string[] {
    const value = selected.value[key];
    if (value === undefined || value === null || value === '' || typeof value === 'boolean') return [];
    return typeof value === 'string' ? [value] : value;
  }
  const one = (key: string) => pick(key)[0] ?? '';
  /** Whether a toggle facet is on: its selection value is `true`, its key absent while off. */
  const on = (key: string) => selected.value[key] === true;

  const selectedRegion = computed(() => regions.value?.find((item) => item.key === one('scope')) ?? null);
  const preset = computed<EventDatePreset>(() => {
    const value = one('when');
    return (EventDatePresetValues as readonly string[]).includes(value) ? (value as EventDatePreset) : 'upcoming';
  });
  const dateRange = computed(() => eventDateRange(preset.value, day.value, customRange.value));
  const dates = computed(() => ({
    ...(dateRange.value.from ? { from: dateRange.value.from } : {}),
    ...(dateRange.value.to ? { to: dateRange.value.to } : {}),
    ...(dateRange.value.includeUndated ? { includeUndated: true } : {}),
  }));

  /** The catalog query: the filters the catalog list and the calendar file take. */
  const eventQuery = computed<EventQuery>(() => {
    const region = selectedRegion.value;
    const country = regions.value?.length
      ? region?.countries?.length === 1 && !region.otherCountries
        ? region.countries[0]!.toUpperCase()
        : ''
      : pick('country').length === 1
        ? pick('country')[0]!
        : '';
    return {
      review: 'published',
      ...dates.value,
      ...(text.value ? { text: text.value } : {}),
      ...(pick('kind').length === 1 ? { kind: pick('kind')[0]! } : {}),
      ...(country ? { country } : {}),
    };
  });
  /** Every picked tracking filter but the dates. */
  const pickedFilters = computed<TrackingQuery>(() => {
    const picked = pick('stage');
    return {
      ...(text.value ? { text: text.value } : {}),
      ...(pick('kind').length ? { kinds: pick('kind') } : {}),
      ...(regions.value?.length
        ? regionQuery(selectedRegion.value, regions.value)
        : pick('country').length
          ? { countries: pick('country') }
          : {}),
      ...(pick('city').length ? { cities: pick('city') } : {}),
      ...(pick('source').length ? { sources: pick('source') } : {}),
      ...(picked.length
        ? { stages: picked }
        : hidden.value.length && !on('hidden')
          ? { excludeStages: hidden.value }
          : {}),
      ...(on('priority') ? { priority: 'high' as const } : {}),
      ...(on('deadline') ? { deadlineBefore: addEventDays(day.value, deadlineFacetDays) } : {}),
    };
  });
  /** The tracking query; the facets read takes it as it is, and the list adds the sort, the cursor and the limit. */
  const trackingQuery = computed<TrackingQuery>(() => ({ ...dates.value, ...pickedFilters.value }));
  /** The base query: nothing the reader chose, the default upcoming view with the hidden stages still left out. */
  const baseQuery = computed<TrackingQuery>(() => ({
    includeUndated: true,
    from: day.value,
    ...(hidden.value.length ? { excludeStages: hidden.value } : {}),
  }));
  const facetsKey = computed(() => (tracking.value ? JSON.stringify(trackingQuery.value) : ''));
  const queryKey = computed(() =>
    JSON.stringify(tracking.value ? [trackingQuery.value, sort.value] : eventQuery.value),
  );
  const filtered = computed(() => Boolean(text.value || Object.keys(selected.value).length));

  const facets = useEventsRequest((signal) => client().getTrackingFacets(catalog(), trackingQuery.value, signal));
  const baseFacets = useEventsRequest((signal) => client().getTrackingFacets(catalog(), baseQuery.value, signal));

  function refresh() {
    if (!tracking.value) return;
    void facets.run();
    void baseFacets.run();
  }

  // --- Facets ---

  const capitalized = (value: string) =>
    value ? value.charAt(0).toLocaleUpperCase(locale.value) + value.slice(1) : value;

  /** Builds a facet's options from the server's counts, keeping a chosen value that the counts no longer list. */
  function countedOptions(
    key: string,
    list: readonly TrackingFacetCount[],
    label: (item: TrackingFacetCount) => string,
  ): FacetOption[] {
    const options = list.map((item) => ({ value: item.value, label: label(item), count: item.count }));
    for (const value of pick(key)) {
      if (!options.some((option) => option.value === value)) {
        options.push({ value, label: label({ value, count: 0 }), count: 0 });
      }
    }
    return options;
  }

  /** Sums the countries facet over a region's country set; an `otherCountries` region takes the unlisted ones. */
  function regionCount(region: EventRegion, list: readonly TrackingFacetCount[]): number {
    const listed = new Set(
      (regions.value ?? []).flatMap((item) => (item.otherCountries ? [] : (item.countries ?? []))).map(upper),
    );
    const own = new Set((region.countries ?? []).map(upper));
    return list.reduce((sum, item) => {
      const code = upper(item.value);
      return (region.otherCountries ? !listed.has(code) : own.has(code)) ? sum + item.count : sum;
    }, 0);
  }

  const countryLabel = (code: string) => `${regionFlag(code)} ${regionName(code, locale.value)}`.trim();
  const allCountries = computed(() =>
    RegionCodes.map((code) => ({ value: code, label: countryLabel(code) })).sort((a, b) =>
      a.label.localeCompare(b.label, locale.value),
    ),
  );

  const facetDefinitions = computed<readonly FacetDefinition[]>(() => {
    const counts = tracking.value ? facets.value.value : null;
    const lang = locale.value;
    const msg = (key: EventsMessageKey, params?: Record<string, string | number>) => eventsText(key, lang, params);
    const definitions: FacetDefinition[] = [];

    if (regions.value?.length) {
      definitions.push({
        key: 'scope',
        label: msg('scope'),
        kind: FacetKind.Single,
        placement: FacetPlacement.Inline,
        options: [
          { value: allScopes, label: msg('all') },
          ...regions.value.map((region) => ({
            value: region.key,
            label: pickText(region.label, lang) || region.key,
            ...(counts ? { count: regionCount(region, counts.countries) } : {}),
          })),
        ],
      });
    }

    const kindOptions = counts
      ? countedOptions('kind', counts.kinds, (item) => EventsLabels.kind(item.value, kinds.value, lang))
      : (kinds.value ?? []).map((item) => ({ value: item.key, label: pickText(item.label, lang) || item.key }));
    if (kindOptions.length) {
      definitions.push({
        key: 'kind',
        label: msg('kind'),
        kind: tracking.value ? FacetKind.Multi : FacetKind.Single,
        placement: FacetPlacement.Inline,
        options: kindOptions,
      });
    }

    if (tracking.value) {
      const stageOptions = counts
        ? countedOptions('stage', counts.stages, (item) => EventsLabels.stage(item.value, stages.value, lang))
        : stages.value.map((item) => ({
            value: item.key,
            label: EventsLabels.stage(item.key, stages.value, lang),
          }));
      if (stageOptions.length) {
        definitions.push({
          key: 'stage',
          label: msg('stage'),
          kind: FacetKind.Multi,
          placement: FacetPlacement.Popover,
          options: stageOptions,
        });
      }
      if (hidden.value.length && !pick('stage').length) {
        definitions.push({
          key: 'hidden',
          label: msg('showHidden', { labels: hiddenLabels.value }),
          kind: FacetKind.Toggle,
          placement: FacetPlacement.Popover,
        });
      }
      if (counts?.cities.length || pick('city').length) {
        definitions.push({
          key: 'city',
          label: msg('city'),
          kind: FacetKind.Multi,
          placement: FacetPlacement.Popover,
          options: countedOptions('city', counts?.cities ?? [], (item) => capitalized(item.value)),
        });
      }
      if (counts?.sources.length || pick('source').length) {
        definitions.push({
          key: 'source',
          label: msg('sourceFacet'),
          kind: FacetKind.Multi,
          placement: FacetPlacement.Popover,
          options: countedOptions('source', counts?.sources ?? [], (item) =>
            item.value === EventsConstants.curatedSource ? msg('sourceCurated') : (item.label ?? item.value),
          ),
        });
      }
      if (!regions.value?.length && (counts?.countries.length || pick('country').length)) {
        definitions.push({
          key: 'country',
          label: msg('country'),
          kind: FacetKind.Multi,
          placement: FacetPlacement.Popover,
          options: countedOptions('country', counts?.countries ?? [], (item) => countryLabel(item.value)),
        });
      }
      definitions.push({
        key: 'priority',
        label: msg('highPriority'),
        kind: FacetKind.Toggle,
        placement: FacetPlacement.Popover,
        ...(counts ? { count: countOf(counts.priorities, 'high') ?? 0 } : {}),
      });
      definitions.push({
        key: 'deadline',
        label: msg('deadlineSoon'),
        kind: FacetKind.Toggle,
        placement: FacetPlacement.Popover,
        ...(counts ? { count: counts.openDeadline.count } : {}),
      });
    } else if (!regions.value?.length) {
      definitions.push({
        key: 'country',
        label: msg('country'),
        kind: FacetKind.Single,
        placement: FacetPlacement.Popover,
        options: allCountries.value,
      });
    }

    definitions.push({
      key: 'when',
      label: msg('dateFacet'),
      kind: FacetKind.Single,
      placement: FacetPlacement.Popover,
      options: EventDatePresetValues.map((value) => ({ value, label: msg(presetKeys[value]) })),
    });
    return definitions;
  });

  const matchCount = computed(() => (tracking.value ? facets.value.value?.total : undefined));
  const totalCount = computed(() =>
    tracking.value && matchCount.value !== undefined ? baseFacets.value.value?.total : undefined,
  );

  function select(next: FacetSelection) {
    const { scope, when, ...rest } = next;
    selected.value = {
      ...rest,
      ...(typeof scope === 'string' && scope !== allScopes ? { scope } : {}),
      ...(typeof when === 'string' && when !== 'upcoming' ? { when } : {}),
    };
  }
  const selection = computed<FacetSelection>({ get: () => selected.value, set: select });

  // --- Pages ---

  function toWindowQuery(from: string, to: string): TrackingQuery {
    return { ...pickedFilters.value, from, to };
  }

  function toRouteQuery(): Record<string, string | string[]> {
    const query: Record<string, string | string[]> = {};
    const list = (param: string, key: string) => {
      if (pick(key).length) query[param] = [...pick(key)];
    };
    if (search.value.trim()) query.q = search.value.trim();
    list('stages', 'stage');
    list('kinds', 'kind');
    list('cities', 'city');
    list('sources', 'source');
    if (one('scope')) query.regions = one('scope');
    if (one('when')) query.when = one('when');
    if (sort.value !== 'starts') query.sort = sort.value;
    return query;
  }

  function fromRouteQuery(query: EventsRouteQuery) {
    const values = (param: string): string[] => {
      const raw = query[param];
      return (typeof raw === 'string' ? [raw] : (raw ?? [])).filter(
        (value): value is string => typeof value === 'string' && value !== '',
      );
    };
    const next: Record<string, string | readonly string[]> = {};
    // The picks the params do not carry (toggles, a country list) stay as they are.
    for (const [key, value] of Object.entries(selected.value)) {
      if (!(routeFacetKeys as readonly string[]).includes(key) && value !== undefined && value !== null) {
        next[key] = value as string | readonly string[];
      }
    }
    const kindValues = values('kinds');
    if (kindValues.length) next.kind = tracking.value ? kindValues : kindValues[0]!;
    if (tracking.value) {
      for (const [param, key] of [
        ['stages', 'stage'],
        ['cities', 'city'],
        ['sources', 'source'],
      ] as const) {
        const list = values(param);
        if (list.length) next[key] = list;
      }
    }
    const scope = values('regions')[0];
    if (scope && regions.value?.some((region) => region.key === scope)) next.scope = scope;
    const when = values('when')[0];
    if (when && when !== 'upcoming' && (EventDatePresetValues as readonly string[]).includes(when)) next.when = when;

    if (canonical(next as FacetSelection) !== canonical(selected.value)) selected.value = next as FacetSelection;
    const order = values('sort')[0];
    sort.value =
      tracking.value && (TrackingSortValues as readonly string[]).includes(order ?? '')
        ? (order as TrackingSort)
        : 'starts';
    const typed = values('q')[0] ?? '';
    search.value = typed;
    text.value = typed.trim();
  }

  // --- Wiring ---

  watch(search, (value) => {
    clearTimeout(timer);
    timer = setTimeout(() => {
      text.value = value.trim();
    }, debounceMs);
  });
  watch(preset, (value) => {
    if (value !== 'custom') customRange.value = null;
  });
  watch([facetsKey, catalog, client], () => {
    if (tracking.value) void facets.run();
  });
  watch([catalog, client, tracking, day, () => hidden.value.join('\u0000')], () => {
    if (tracking.value) void baseFacets.run();
  });
  function start() {
    if (tracking.value && !toValue(options.workflow)) void workflowRequest.run();
    refresh();
  }
  if (getCurrentInstance()) onMounted(start);
  else start();
  onScopeDispose(() => clearTimeout(timer));

  return {
    search,
    selection,
    sort,
    customRange,
    preset,
    tracking,
    workflow,
    facetDefinitions,
    matchCount,
    totalCount,
    eventQuery,
    trackingQuery,
    queryKey,
    filtered,
    select,
    refresh,
    toWindowQuery,
    toRouteQuery,
    fromRouteQuery,
  };
}
