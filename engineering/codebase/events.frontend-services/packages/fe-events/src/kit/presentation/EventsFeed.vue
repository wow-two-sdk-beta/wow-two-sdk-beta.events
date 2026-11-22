<script setup lang="ts">
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { useAdaptiveList } from '@wow-two-beta/fe/foundation/virtualization';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { FacetBar } from '@wow-two-beta/fe/presentation/forms';
import { computed, nextTick, onMounted, shallowRef, useTemplateRef, watch } from 'vue';
import {
  type CatalogKindModel,
  type EventImage,
  type EventModel,
  type TrackingModel,
  type TrackingSpec,
  type WorkflowModel,
} from '../../contracts';
import { downloadEventsCalendar } from '../calendar/DownloadEventsCalendar';
import type { EventsClient, EventsResult } from '../client/CreateEventsClient';
import type { EventsFailure } from '../client/EventsFailure';
import { deadlineDaysLeft, eventGroupHeading, groupEventsByMonth, localIsoDate } from '../dates/EventDates';
import { eventTitle } from '../facts/EventFactsText';
import type { EventProposalOptions } from '../facts/EventProposalOptions';
import type { EventRegion } from '../facts/EventRegions';
import { useEventsFilters, type EventsFilters } from '../hooks/UseEventsFilters';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import { eventsFacetBarLabels } from '../text/EventsFacetBarLabels';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import EventMediaCard from './EventMediaCard.vue';
import {
  type EventCardActionsSlot,
  type EventCardDetailSlot,
  type EventCardFallbackSlot,
  type EventCardMediaSlot,
  type EventCardMetaSlot,
  type EventCardPillsSlot,
  type EventCardTitleSlot,
} from './EventMediaCardSlots';
import EventProposalForm from './EventProposalForm.vue';
import EventsDeadlines from './EventsDeadlines.vue';
import EventsFeedActions from './EventsFeedActions.vue';
import { feedFacetBarClass, feedPillClass } from './EventsFeedStyles';
import FailureNotice from './FailureNotice.vue';

interface FeedItem {
  readonly event: EventModel;
  readonly tracking: TrackingModel | null;
}

interface FeedPage {
  readonly items: readonly FeedItem[];
  readonly nextCursor: string | null;
}

/**
 * Lists a catalog's events as image-first `EventMediaCard`s in a responsive grid, behind the fe sdk's `FacetBar`: the
 * match pill ("12 of 72", tracking only), a search, inline scope chips (the consumer's `regions`) and kind chips, a
 * "Filters" popover (stage, city, source, the toggles "High priority", "Deadline within 30 days" and "Show {hidden
 * stages}", and the date presets: upcoming by default, this month, next 3 months, this year, past, a custom range)
 * whose picks show as removable chips, a sort (start date, recently added, nearest deadline) and — at the end — the proposal button and an
 * overflow menu with the calendar download. The list scrolls without a button: `useAdaptiveList` loads the next cursor
 * ahead of the reader. Under the `starts` sort each month has a divider, undated events last; under the other sorts
 * the grid is flat. With `tracking` (PG01) it lists the organization's tracking: each card carries its stage (an
 * untracked event the default stage), priority and quick stage actions; the chips carry server counts (one
 * `getTrackingFacets` read per query change, each facet counted without its own selection), and attention pills
 * between the toolbar and the grid — "{n} deadlines in 14 days", expanding to `EventsDeadlines`, plus the consumer's
 * `attention` pills, expanding to the `attention` slot — show only above zero. Without `tracking` it lists the catalog
 * on the same toolbar with no counts, no stage, priority, source or city facets and no sort. A member's proposal
 * publishes at once in a private catalog and the feed reloads to show it; in a shared or public one it waits for review.
 * A `card-{zone}` slot (`card-media`, `card-fallback`, `card-pills`, `card-actions`, `card-title`, `card-meta`,
 * `card-detail`) is forwarded to every card's slot of that zone with the card's own slot props; a zone left empty keeps
 * the card's content.
 *
 * The filter state lives in `useEventsFilters`. The feed calls it from its own props; a page that hosts the filter bar in
 * its own card (the calendar beside the feed) calls it itself, renders the `FacetBar` from `facetDefinitions`, `search`,
 * `selection`, `matchCount` and `totalCount`, and passes the result as `filters`. Then the feed lists from that state,
 * renders no toolbar (no `events-toolbar`) and reads no facet counts; the custom range picker (the `custom` preset only),
 * the sort (tracking only), the proposal button and the overflow menu sit in a right-aligned `events-actions` row above
 * the attention pills, which `actionsTo` (a CSS selector) moves into the page's card.
 */
defineOptions({ name: 'EventsFeed' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    locale?: EventsLocale;
    /** The person's calendar day, `YYYY-MM-DD`; the browser's by default. */
    today?: string;
    /** The catalog's kinds, for the filter and the labels. */
    kinds?: readonly CatalogKindModel[];
    pageSize?: number;
    /** Whether the person may propose an event (a member of the catalog's readers). */
    canPropose?: boolean;
    /** Builds an event's page link; the cards emit `open` either way. */
    eventHref?: (id: string) => string;
    /** Whether to list the organization's tracking (stages, priority, counts) instead of the catalog's events. */
    tracking?: boolean;
    /** The organization's workflow; read with `client.getWorkflow()` when absent and `tracking` is on. */
    workflow?: Pick<WorkflowModel, 'stages' | 'defaultStage'> | null;
    /** Stages left out unless their own filter or the "Show …" toggle asks (Ocharo: skipped, rejected). */
    hiddenStages?: readonly string[];
    /** The consumer's regions: scope chips over country sets (with counts, summed from the countries facet). */
    regions?: readonly EventRegion[];
    /** Stages offered as one-click actions on cards at the default stage (Ocharo: shortlisted, skipped). */
    quickStages?: readonly string[];
    /** Whether the person may write tracking (`events:track`), for the quick actions. */
    canTrack?: boolean;
    /** Shapes the proposal form. */
    proposal?: EventProposalOptions;
    /** The viewer's IANA zone, to name a start time's zone only when it differs; the browser's by default. */
    viewerZone?: string;
    /**
     * The consumer's attention pills, shown between the toolbar and the grid after the built-in deadlines pill and hidden
     * at a `count` of 0. A pill expands to the `attention` slot, which gets its `key` and a `close` function.
     */
    attention?: readonly { key: string; label: string; count: number }[];
    /** Forwarded to every card: the source to show for an event's image, such as its stored copy; null keeps the `url`. */
    imageSrc?: (event: EventModel, image: EventImage, index: number) => string | null;
    /**
     * The filter state of a page that hosts the filter bar itself (`useEventsFilters`). The feed lists from it, renders
     * no `FacetBar` and starts no facet requests of its own; it shows its own controls in the `events-actions` row.
     */
    filters?: EventsFilters;
    /** A CSS selector for the element the `events-actions` row moves into, so a page's card can host it; with `filters`. */
    actionsTo?: string;
  }>(),
  {
    locale: 'en',
    today: undefined,
    kinds: undefined,
    pageSize: 50,
    canPropose: false,
    eventHref: undefined,
    tracking: false,
    workflow: undefined,
    hiddenStages: () => [],
    regions: undefined,
    quickStages: () => [],
    canTrack: false,
    proposal: () => ({}),
    viewerZone: undefined,
    attention: () => [],
    imageSrc: undefined,
    filters: undefined,
    actionsTo: undefined,
  },
);

const emit = defineEmits<{
  open: [id: string];
  /** Fires with the event a proposal created: published (a private catalog) or a candidate awaiting review. */
  proposed: [event: EventModel];
  /** Fires with the tracking a quick stage action wrote. */
  tracked: [tracking: TrackingModel];
}>();

const slots = defineSlots<{
  /** The body of a consumer attention pill once it is expanded. */
  attention?(props: { key: string; close: () => void }): unknown;
  /** Forwarded to each card's `media` slot: replaces the curated image. */
  'card-media'?(props: EventCardMediaSlot): unknown;
  /** Forwarded to each card's `fallback` slot: replaces the date tile. */
  'card-fallback'?(props: EventCardFallbackSlot): unknown;
  /** Forwarded to each card's `pills` slot: replaces the top-start pills. */
  'card-pills'?(props: EventCardPillsSlot): unknown;
  /** Forwarded to each card's `actions` slot: replaces the top-end mark and menu. */
  'card-actions'?(props: EventCardActionsSlot): unknown;
  /** Forwarded to each card's `title` slot: replaces the heading's content. */
  'card-title'?(props: EventCardTitleSlot): unknown;
  /** Forwarded to each card's `meta` slot: replaces the dates · city line. */
  'card-meta'?(props: EventCardMetaSlot): unknown;
  /** Forwarded to each card's `detail` slot: replaces the deadline-or-sources line. */
  'card-detail'?(props: EventCardDetailSlot): unknown;
}>();

const deadlinesPill = 'deadlines';
const deadlinesWindow = 14;

const day = computed(() => props.today ?? localIsoDate());
const openPill = shallowRef<string | null>(null);
const settled = shallowRef(false);
const proposing = shallowRef(false);
const proposedTitle = shallowRef<string | null>(null);
const proposedPublished = shallowRef(false);
const trackingNotSaved = shallowRef(false);
const trackNotice = shallowRef(false);
const calendarFailure = shallowRef<EventsFailure | null>(null);
const calendarPending = shallowRef(false);
const downloadFailed = shallowRef(false);
const proposalSection = useTemplateRef<HTMLElement>('proposalForm');

// The filter state: a page's (`filters`), else the feed's own. With a page's, the feed's own reads nothing.
const own = useEventsFilters({
  client: () => props.client,
  catalog: () => props.catalog,
  locale: () => props.locale,
  today: () => props.today,
  kinds: () => props.kinds,
  tracking: () => props.tracking && !props.filters,
  workflow: () => props.workflow,
  hiddenStages: () => props.hiddenStages,
  regions: () => props.regions,
});
const { search, selection, facetDefinitions, matchCount, totalCount, select } = own;
const state = computed(() => props.filters ?? own);
const tracked = computed(() => state.value.tracking.value);
const sortChoice = computed({
  get: () => state.value.sort.value,
  set: (value) => {
    state.value.sort.value = value;
  },
});
const customPreset = computed(() => state.value.preset.value === 'custom');

const trackRequest = useEventsRequest((signal, eventId: string, spec: TrackingSpec) =>
  props.client.putTracking(props.catalog, eventId, spec, signal),
);

const flow = computed(() => props.workflow ?? state.value.workflow.value);
const stages = computed(() => flow.value?.stages ?? []);
const defaultStage = computed(() => (tracked.value ? flow.value?.defaultStage : undefined));

const summary = useEventsRequest((signal) =>
  props.client.getTrackingSummary(props.catalog, { today: day.value, deadlineKinds: ['application'] }, signal),
);

/** Reads the counts again after a write: the filter state's facets, and the deadlines. */
function loadCounts() {
  if (!tracked.value) return;
  state.value.refresh();
  void summary.run();
}

async function fetchPage(cursor: string | null, signal: AbortSignal): EventsResult<FeedPage> {
  const page = { limit: props.pageSize, ...(cursor ? { cursor } : {}) };
  if (tracked.value) {
    const result = await props.client.listTracking(
      props.catalog,
      { ...state.value.trackingQuery.value, sort: state.value.sort.value, ...page },
      signal,
    );
    return result.ok
      ? ResultExtensions.ok({
          items: result.value.items.map((item) => ({ event: item.event, tracking: item.tracking ?? null })),
          nextCursor: result.value.nextCursor ?? null,
        })
      : result;
  }
  const result = await props.client.listEvents(props.catalog, { ...state.value.eventQuery.value, ...page }, signal);
  return result.ok
    ? ResultExtensions.ok({
        items: result.value.items.map((event) => ({ event, tracking: null })),
        nextCursor: result.value.nextCursor ?? null,
      })
    : result;
}

const adaptive = useAdaptiveList<FeedItem, string>({
  load: async (cursor, signal) => {
    const result = await fetchPage(cursor, signal);
    if (!signal.aborted) settled.value = true;
    if (!result.ok) {
      if (result.failure.reason === 'cancelled') return { items: [], next: null, hasMore: false };
      throw result.failure;
    }
    return { items: result.value.items, next: result.value.nextCursor, hasMore: result.value.nextCursor !== null };
  },
});
const { sentinel } = adaptive;
const items = adaptive.items;
const listFailure = computed<EventsFailure | null>(() => {
  const error = adaptive.error.value;
  return error && typeof error === 'object' && 'reason' in error ? (error as EventsFailure) : null;
});

function reload() {
  settled.value = false;
  return adaptive.reset();
}

const byMonth = computed(() => !tracked.value || state.value.sort.value === 'starts');
const groups = computed(() =>
  byMonth.value
    ? groupEventsByMonth(items.value.map((item) => ({ facts: item.event.facts, item }))).map((group) => ({
        key: group.key,
        items: group.events.map((entry) => entry.item),
      }))
    : items.value.length
      ? [{ key: 'all', items: items.value }]
      : [],
);

function pickRange(range: { start: { toString(): string }; end: { toString(): string } } | null) {
  state.value.customRange.value = range ? { from: range.start.toString(), to: range.end.toString() } : null;
}

// --- Attention pills ---

const soonDeadlines = computed(
  () =>
    (summary.value.value?.upcomingDeadlines ?? []).filter((item) => {
      const left = item.deadline.kind === 'application' ? deadlineDaysLeft(item.deadline, day.value) : null;
      return left !== null && left >= 0 && left <= deadlinesWindow;
    }).length,
);
const pills = computed(() => [
  ...(tracked.value && soonDeadlines.value > 0
    ? [
        {
          key: deadlinesPill,
          label: eventsText(soonDeadlines.value === 1 ? 'deadlinesSoonOne' : 'deadlinesSoon', props.locale, {
            count: soonDeadlines.value,
          }),
        },
      ]
    : []),
  ...props.attention.filter((item) => item.count > 0).map((item) => ({ key: item.key, label: item.label })),
]);
const expanded = computed(() => pills.value.find((item) => item.key === openPill.value)?.key ?? null);
const togglePill = (key: string) => {
  openPill.value = openPill.value === key ? null : key;
};
const closePill = () => {
  openPill.value = null;
};

// --- Wiring ---

watch([() => state.value.queryKey.value, () => props.catalog, () => props.client, tracked], () => void reload());
watch([() => props.catalog, () => props.client, tracked, day], () => {
  if (tracked.value) void summary.run();
});
onMounted(() => {
  if (tracked.value) void summary.run();
});

async function downloadCalendar() {
  calendarPending.value = true;
  const result = await downloadEventsCalendar({
    client: props.client,
    catalog: props.catalog,
    query: state.value.eventQuery.value,
  });
  calendarPending.value = false;
  calendarFailure.value = result.ok ? null : result.failure;
  downloadFailed.value = result.ok && !result.value;
}

async function track(eventId: string, next: string) {
  trackNotice.value = false;
  const current = items.value.find((item) => item.event.id === eventId)?.tracking ?? null;
  const spec: TrackingSpec = current
    ? {
        stage: next,
        priority: current.priority,
        assignee: current.assignee ?? null,
        notes: current.notes ?? '',
        ...(current.fields !== undefined ? { fields: current.fields } : {}),
        references: current.references ?? [],
        expectedRevision: current.revision,
      }
    : { stage: next, expectedRevision: null };
  const saved = await trackRequest.run(eventId, spec);
  if (saved) emit('tracked', saved);
  else if (trackRequest.failure.value?.reason === 'conflict' || trackRequest.failure.value?.reason === 'notFound') {
    trackNotice.value = true;
    trackRequest.failure.value = null;
  } else return;
  loadCounts();
  await reload();
}

/** Opens the proposal form and brings it to the reader: on a phone it lands below the fold, where the tap looks dead. */
async function openProposal() {
  proposing.value = true;
  proposedTitle.value = null;
  await nextTick();
  const form = proposalSection.value;
  if (!form) return;
  const calm = typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  form.scrollIntoView?.({ block: 'start', behavior: calm ? 'auto' : 'smooth' });
  // The button the reader tapped has left the toolbar, so focus goes to the form's first field, without a second scroll.
  form.querySelector<HTMLElement>('input, select, textarea')?.focus({ preventScroll: true });
}

function proposed(event: EventModel, written: TrackingModel | null = null) {
  proposing.value = false;
  proposedTitle.value = eventTitle(event.facts, props.locale);
  proposedPublished.value = event.review === 'published';
  trackingNotSaved.value = Boolean(props.proposal.initialTracking) && written === null;
  emit('proposed', event);
  if (proposedPublished.value) {
    loadCounts();
    void reload();
  }
}
</script>

<template>
  <section class="space-y-4" aria-labelledby="events-feed-title">
    <h2 id="events-feed-title" class="sr-only">{{ eventsText('eventsTitle', locale) }}</h2>

    <FacetBar
      v-if="!filters"
      v-model:search="search"
      :facets="facetDefinitions"
      :model-value="selection"
      :match-count="matchCount"
      :total-count="totalCount"
      v-bind="eventsFacetBarLabels(locale)"
      :class="feedFacetBarClass"
      data-test="events-toolbar"
      @update:model-value="select"
    >
      <template #trailing>
        <EventsFeedActions
          v-model:sort="sortChoice"
          :locale="locale"
          :custom="customPreset"
          :tracking="tracked"
          :proposable="canPropose && !proposing"
          :calendar-pending="calendarPending"
          @range="pickRange"
          @propose="openProposal"
          @download="downloadCalendar"
        />
      </template>
    </FacetBar>
    <Teleport v-else defer :to="actionsTo" :disabled="!actionsTo">
      <div class="flex flex-wrap items-center justify-end gap-2" data-test="events-actions">
        <EventsFeedActions
          v-model:sort="sortChoice"
          :locale="locale"
          :custom="customPreset"
          :tracking="tracked"
          :proposable="canPropose && !proposing"
          :calendar-pending="calendarPending"
          @range="pickRange"
          @propose="openProposal"
          @download="downloadCalendar"
        />
      </div>
    </Teleport>

    <div v-if="pills.length" class="flex flex-wrap items-center gap-2" data-test="attention-pills">
      <button
        v-for="pill in pills"
        :key="pill.key"
        type="button"
        :class="[
          feedPillClass,
          expanded === pill.key ? 'border-brand bg-brand text-brand-foreground' : 'border-border bg-background',
        ]"
        :aria-expanded="expanded === pill.key"
        :data-pill="pill.key"
        @click="togglePill(pill.key)"
      >
        {{ pill.label }}
      </button>
    </div>
    <div v-if="expanded" data-test="attention-panel" :data-pill="expanded">
      <EventsDeadlines
        v-if="expanded === deadlinesPill"
        :client="client"
        :catalog="catalog"
        :locale="locale"
        :today="day"
        :stages="stages"
        :deadline-kinds="['application']"
        :event-href="eventHref"
        @open="(id) => emit('open', id)"
      />
      <slot v-else :key="expanded" name="attention" :close="closePill"></slot>
    </div>

    <p
      v-if="proposedTitle"
      class="rounded-md bg-info-soft px-3 py-2 text-sm text-info-soft-foreground"
      role="status"
      data-test="proposal-sent"
    >
      {{ eventsText(proposedPublished ? 'proposedPublished' : 'proposed', locale, { title: proposedTitle }) }}
      <template v-if="trackingNotSaved"> {{ eventsText('trackingNotSaved', locale) }}</template>
    </p>
    <section
      v-if="proposing"
      ref="proposalForm"
      class="scroll-mt-20 rounded-lg border border-border p-4"
      :aria-label="eventsText('proposeEvent', locale)"
    >
      <h3 class="mb-3 font-semibold">{{ eventsText('proposeEvent', locale) }}</h3>
      <EventProposalForm
        :client="client"
        :catalog="catalog"
        :locale="locale"
        :kinds="kinds"
        :regions="regions"
        :initial-tracking="proposal.initialTracking"
        :source-field="proposal.sourceField"
        :with-deadline="proposal.withDeadline ?? false"
        :with-found-at="proposal.withFoundAt ?? false"
        :private-catalog="proposal.privateCatalog ?? false"
        @proposed="proposed"
        @cancel="proposing = false"
      />
    </section>

    <FailureNotice v-if="calendarFailure" :failure="calendarFailure" :locale="locale" />
    <p v-else-if="downloadFailed" class="text-sm text-destructive" role="alert">
      {{ eventsText('downloadFailed', locale) }}
    </p>
    <p v-if="trackNotice" class="text-sm text-warning" role="status" data-test="track-changed">
      {{ eventsText('trackChanged', locale) }}
    </p>
    <FailureNotice v-if="trackRequest.failure.value" :failure="trackRequest.failure.value" :locale="locale" />

    <div class="min-h-40 space-y-6" :aria-busy="adaptive.isLoading.value">
      <div v-if="!items.length && !adaptive.error.value && (adaptive.isLoading.value || !settled)" class="space-y-3">
        <span role="status" class="sr-only">{{ eventsText('loadingEvents', locale) }}</span>
        <div class="space-y-3" aria-hidden="true">
          <SkeletonState v-for="index in 3" :key="index" class="h-24 w-full" />
        </div>
      </div>
      <template v-else>
        <section
          v-for="group in groups"
          :key="group.key"
          class="space-y-3"
          :aria-labelledby="byMonth ? `events-group-${group.key}` : undefined"
          :aria-label="byMonth ? undefined : eventsText('eventsTitle', locale)"
          data-test="event-group"
        >
          <h3
            v-if="byMonth"
            :id="`events-group-${group.key}`"
            class="text-sm font-semibold uppercase tracking-wide text-muted-foreground"
          >
            {{ eventGroupHeading(group.key, locale) }}
          </h3>
          <ul class="grid grid-cols-[repeat(auto-fill,minmax(min(100%,17rem),1fr))] gap-3" data-test="event-cards">
            <li v-for="item in group.items" :key="item.event.id" class="min-w-0">
              <EventMediaCard
                :event="item.event"
                :locale="locale"
                :today="day"
                :kinds="kinds"
                :href="eventHref?.(item.event.id)"
                :tracking="item.tracking"
                :stages="stages"
                :default-stage="defaultStage"
                :quick-stages="quickStages"
                :can-track="tracked && canTrack"
                :track-pending="trackRequest.pending.value"
                :viewer-zone="viewerZone"
                :image-src="imageSrc"
                @open="(id) => emit('open', id)"
                @track="track"
              >
                <template v-if="slots['card-media']" #media="scope">
                  <slot name="card-media" v-bind="scope"></slot>
                </template>
                <template v-if="slots['card-fallback']" #fallback="scope">
                  <slot name="card-fallback" v-bind="scope"></slot>
                </template>
                <template v-if="slots['card-pills']" #pills="scope">
                  <slot name="card-pills" v-bind="scope"></slot>
                </template>
                <template v-if="slots['card-actions']" #actions="scope">
                  <slot name="card-actions" v-bind="scope"></slot>
                </template>
                <template v-if="slots['card-title']" #title="scope">
                  <slot name="card-title" v-bind="scope"></slot>
                </template>
                <template v-if="slots['card-meta']" #meta="scope">
                  <slot name="card-meta" v-bind="scope"></slot>
                </template>
                <template v-if="slots['card-detail']" #detail="scope">
                  <slot name="card-detail" v-bind="scope"></slot>
                </template>
              </EventMediaCard>
            </li>
          </ul>
        </section>
        <FailureNotice v-if="listFailure" :failure="listFailure" :locale="locale" retry @retry="adaptive.loadMore()" />
        <p v-else-if="adaptive.error.value" class="text-sm text-destructive" role="alert">
          {{ eventsText('failureUnexpected', locale) }}
        </p>
        <p
          v-if="!groups.length && !adaptive.isLoading.value && !adaptive.error.value"
          class="py-8 text-center text-sm text-muted-foreground"
        >
          {{ state.filtered.value ? eventsText('noEventsMatch', locale) : eventsText('noUpcoming', locale) }}
        </p>
        <div v-if="items.length && adaptive.isLoading.value" class="space-y-3">
          <span role="status" class="sr-only">{{ eventsText('loadingEvents', locale) }}</span>
          <div aria-hidden="true"><SkeletonState class="h-24 w-full" /></div>
        </div>
      </template>
      <div ref="sentinel" class="h-px" aria-hidden="true" data-test="feed-sentinel"></div>
    </div>
  </section>
</template>
