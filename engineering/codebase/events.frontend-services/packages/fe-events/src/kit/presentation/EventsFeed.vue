<script setup lang="ts">
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { TextInput } from '@wow-two-beta/fe/presentation/forms';
import { computed, onMounted, onScopeDispose, shallowRef, watch } from 'vue';
import type {
  CatalogKindModel,
  EventModel,
  EventQuery,
  TrackingModel,
  TrackingQuery,
  TrackingSpec,
  TrackingSummaryModel,
  WorkflowModel,
} from '../../contracts';
import { downloadEventsCalendar } from '../calendar/DownloadEventsCalendar';
import type { EventsClient, EventsResult } from '../client/CreateEventsClient';
import type { EventsFailure } from '../client/EventsFailure';
import { eventGroupHeading, groupEventsByMonth, localIsoDate } from '../dates/EventDates';
import { eventTitle } from '../facts/EventFactsText';
import type { EventProposalOptions } from '../facts/EventProposalOptions';
import { regionQuery, type EventRegion } from '../facts/EventRegions';
import type { TrackingShownField } from '../facts/TrackingFields';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import EventCard from './EventCard.vue';
import EventMediaCard from './EventMediaCard.vue';
import EventProposalForm from './EventProposalForm.vue';
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
 * Lists a catalog's events grouped by the month they start in, undated last, with search, kind and country (or region)
 * filters, past events on request, a calendar download, and — for members — a proposal: in a private catalog it publishes
 * at once and the feed reloads to show it; in a shared or public one it waits for review. With `tracking`, it lists the
 * organization's tracking instead (PG01): each card carries its stage (an untracked one the default stage), priority and
 * chosen fields; a stage filter with counts, stages hidden by default, and quick stage actions. With `variant="cards"`,
 * each month's events are image-first `EventMediaCard`s in a responsive grid — the image or a date tile, kind and stage
 * pills, a stage menu and priority mark, the title, dates, city and next deadline; scope, cost, organizer and the review
 * state stay off the card. The default, `list`, is the `EventCard` rows.
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
    /** Whether to list the organization's tracking (stages, priority, fields) instead of the catalog's events. */
    tracking?: boolean;
    /** The organization's workflow; read with `client.getWorkflow()` when absent and `tracking` is on. */
    workflow?: Pick<WorkflowModel, 'stages' | 'defaultStage'> | null;
    /** Stages left out unless their own filter or the "Show …" toggle asks (Ocharo: skipped, rejected). */
    hiddenStages?: readonly string[];
    /** The consumer's regions: chips over country sets in place of the country input, and each card's scope. */
    regions?: readonly EventRegion[];
    /** Stages offered as one-click actions on cards at the default stage (Ocharo: shortlisted, skipped). */
    quickStages?: readonly string[];
    /** Whether the person may write tracking (`events:track`), for the quick actions. */
    canTrack?: boolean;
    /** Tracking fields shown read-only on each card (Ocharo: its cost). */
    shownFields?: readonly TrackingShownField[];
    /** Shapes the proposal form. */
    proposal?: EventProposalOptions;
    /** The viewer's IANA zone, to name a start time's zone only when it differs; the browser's by default. */
    viewerZone?: string;
    /** How each month's events show: `list` rows (`EventCard`, the default) or image-first `cards` (`EventMediaCard`). */
    variant?: 'list' | 'cards';
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
    shownFields: () => [],
    proposal: () => ({}),
    viewerZone: undefined,
    variant: 'list',
  },
);

const emit = defineEmits<{
  open: [id: string];
  /** Fires with the event a proposal created: published (a private catalog) or a candidate awaiting review. */
  proposed: [event: EventModel];
  /** Fires with the tracking a quick stage action wrote. */
  tracked: [tracking: TrackingModel];
}>();

const day = computed(() => props.today ?? localIsoDate());
const search = shallowRef('');
const text = shallowRef('');
const kind = shallowRef('');
const country = shallowRef('');
const region = shallowRef('');
const stage = shallowRef('');
const showHidden = shallowRef(false);
const showPast = shallowRef(false);
const items = shallowRef<readonly FeedItem[]>([]);
const nextCursor = shallowRef<string | null>(null);
const proposing = shallowRef(false);
const proposedTitle = shallowRef<string | null>(null);
const proposedPublished = shallowRef(false);
const trackingNotSaved = shallowRef(false);
const trackNotice = shallowRef(false);
const calendarFailure = shallowRef<EventsFailure | null>(null);
const calendarPending = shallowRef(false);
const downloadFailed = shallowRef(false);
let timer: ReturnType<typeof setTimeout> | undefined;

const workflowRequest = useEventsRequest((signal) => props.client.getWorkflow(signal));
const summary = useEventsRequest((signal) => props.client.getTrackingSummary(props.catalog, day.value, signal));
const trackRequest = useEventsRequest((signal, eventId: string, spec: TrackingSpec) =>
  props.client.putTracking(props.catalog, eventId, spec, signal),
);

const flow = computed(() => props.workflow ?? workflowRequest.value.value);
const stages = computed(() => flow.value?.stages ?? []);
const defaultStage = computed(() => (props.tracking ? flow.value?.defaultStage : undefined));
const hidden = computed(() => props.hiddenStages.filter((key) => key !== ''));
const selectedRegion = computed(() => props.regions?.find((item) => item.key === region.value) ?? null);

/** Counts a stage with the untracked events added to the default stage, as the tracking list reads them. */
function stageCount(counts: TrackingSummaryModel | null, key: string): number {
  if (!counts) return 0;
  return (counts.byStage[key] ?? 0) + (key === flow.value?.defaultStage ? (counts.untracked ?? 0) : 0);
}
const stageOptions = computed(() =>
  stages.value.map((item) => ({
    value: item.key,
    label: `${EventsLabels.stage(item.key, stages.value, props.locale)} (${stageCount(summary.value.value, item.key)})`,
  })),
);
const totalCount = computed(() => {
  const counts = summary.value.value;
  if (!counts) return 0;
  return Object.values(counts.byStage).reduce((sum, value) => sum + value, 0) + (counts.untracked ?? 0);
});
const hiddenLabels = computed(() =>
  hidden.value.map((key) => EventsLabels.stage(key, stages.value, props.locale)).join(', '),
);

const countryFilter = computed(() =>
  country.value.trim().length === 2 ? { country: country.value.trim().toUpperCase() } : {},
);
const eventFilter = computed<EventQuery>(() => ({
  review: 'published',
  includeUndated: true,
  ...(text.value ? { text: text.value } : {}),
  ...(kind.value ? { kind: kind.value } : {}),
  ...(props.regions?.length
    ? selectedRegion.value?.countries?.length === 1 && !selectedRegion.value.otherCountries
      ? { country: selectedRegion.value.countries[0]!.toUpperCase() }
      : {}
    : countryFilter.value),
  ...(showPast.value ? {} : { from: day.value }),
}));
const trackingFilter = computed<TrackingQuery>(() => ({
  includeUndated: true,
  ...(text.value ? { text: text.value } : {}),
  ...(kind.value ? { kind: kind.value } : {}),
  ...(props.regions?.length ? regionQuery(selectedRegion.value, props.regions) : countryFilter.value),
  ...(stage.value
    ? { stage: stage.value }
    : hidden.value.length && !showHidden.value
      ? { excludeStages: hidden.value }
      : {}),
  ...(showPast.value ? {} : { from: day.value }),
}));
const filterKey = computed(() => JSON.stringify(props.tracking ? trackingFilter.value : eventFilter.value));
const filtered = computed(() =>
  Boolean(text.value || kind.value || country.value.trim() || region.value || stage.value),
);

const list = useEventsRequest(async (signal, cursor: string | null): EventsResult<FeedPage> => {
  const page = { limit: props.pageSize, ...(cursor ? { cursor } : {}) };
  if (props.tracking) {
    const result = await props.client.listTracking(props.catalog, { ...trackingFilter.value, ...page }, signal);
    return result.ok
      ? ResultExtensions.ok({
          items: result.value.items.map((item) => ({ event: item.event, tracking: item.tracking ?? null })),
          nextCursor: result.value.nextCursor ?? null,
        })
      : result;
  }
  const result = await props.client.listEvents(props.catalog, { ...eventFilter.value, ...page }, signal);
  return result.ok
    ? ResultExtensions.ok({
        items: result.value.items.map((event) => ({ event, tracking: null })),
        nextCursor: result.value.nextCursor ?? null,
      })
    : result;
});

async function load(cursor: string | null = null) {
  const page = await list.run(cursor);
  if (!page) return;
  items.value = cursor ? [...items.value, ...page.items] : page.items;
  nextCursor.value = page.nextCursor;
}

function loadSummary() {
  if (props.tracking) void summary.run();
}

const groups = computed(() =>
  groupEventsByMonth(items.value.map((item) => ({ facts: item.event.facts, item }))).map((group) => ({
    key: group.key,
    items: group.events.map((entry) => entry.item),
  })),
);
const kindOptions = computed(() =>
  (props.kinds ?? []).map((item) => ({ value: item.key, label: pickText(item.label, props.locale) || item.key })),
);

watch(search, (value) => {
  clearTimeout(timer);
  timer = setTimeout(() => {
    text.value = value.trim();
  }, 250);
});
watch([filterKey, () => props.catalog, () => props.client, () => props.tracking], () => {
  items.value = [];
  void load();
});
watch([() => props.catalog, () => props.client, () => props.tracking, day], loadSummary);
onMounted(() => {
  if (props.tracking && !props.workflow) void workflowRequest.run();
  loadSummary();
  void load();
});
onScopeDispose(() => clearTimeout(timer));

async function downloadCalendar() {
  calendarPending.value = true;
  const result = await downloadEventsCalendar({
    client: props.client,
    catalog: props.catalog,
    query: eventFilter.value,
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
  loadSummary();
  await load();
}

function proposed(event: EventModel, written: TrackingModel | null = null) {
  proposing.value = false;
  proposedTitle.value = eventTitle(event.facts, props.locale);
  proposedPublished.value = event.review === 'published';
  trackingNotSaved.value = Boolean(props.proposal.initialTracking) && written === null;
  emit('proposed', event);
  if (proposedPublished.value) {
    loadSummary();
    void load();
  }
}
</script>

<template>
  <section class="space-y-4" aria-labelledby="events-feed-title">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 id="events-feed-title" class="text-lg font-semibold">{{ eventsText('eventsTitle', locale) }}</h2>
      <div class="flex flex-wrap gap-2">
        <Button size="sm" variant="ghost" :is-loading="calendarPending" @click="downloadCalendar">
          {{ eventsText('downloadCalendar', locale) }}
        </Button>
        <Button
          v-if="canPropose && !proposing"
          size="sm"
          variant="outline"
          @click="
            proposing = true;
            proposedTitle = null;
          "
        >
          {{ eventsText('proposeEvent', locale) }}
        </Button>
      </div>
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
      class="rounded-lg border border-border p-4"
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

    <div class="flex flex-wrap items-end gap-3" role="search" :aria-label="eventsText('searchEvents', locale)">
      <label class="flex min-w-48 flex-1 flex-col gap-1 text-sm">
        <span class="text-muted-foreground">{{ eventsText('searchEvents', locale) }}</span>
        <input
          v-model="search"
          name="search"
          type="search"
          class="rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
          :placeholder="eventsText('searchPlaceholder', locale)"
          autocomplete="off"
        />
      </label>
      <label v-if="tracking && stageOptions.length" class="flex flex-col gap-1 text-sm">
        <span class="text-muted-foreground">{{ eventsText('stage', locale) }}</span>
        <select v-model="stage" name="stage" class="rounded-md border border-border bg-background px-3 py-2 text-sm">
          <option value="">{{ eventsText('allStages', locale, { count: totalCount }) }}</option>
          <option v-for="item in stageOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
        </select>
      </label>
      <label v-if="kindOptions.length" class="flex flex-col gap-1 text-sm">
        <span class="text-muted-foreground">{{ eventsText('kind', locale) }}</span>
        <select v-model="kind" name="kind" class="rounded-md border border-border bg-background px-3 py-2 text-sm">
          <option value="">{{ eventsText('allKinds', locale) }}</option>
          <option v-for="item in kindOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
        </select>
      </label>
      <label v-if="!regions?.length" class="flex w-28 flex-col gap-1 text-sm">
        <span class="text-muted-foreground">{{ eventsText('country', locale) }}</span>
        <TextInput v-model="country" name="country" :maxlength="2" placeholder="UZ" autocomplete="off" />
      </label>
      <label class="flex items-center gap-2 pb-2 text-sm">
        <input v-model="showPast" name="showPast" type="checkbox" />
        <span>{{ eventsText('showPast', locale) }}</span>
      </label>
      <label v-if="tracking && hidden.length && !stage" class="flex items-center gap-2 pb-2 text-sm">
        <input v-model="showHidden" name="showHidden" type="checkbox" />
        <span>{{ eventsText('showHidden', locale, { labels: hiddenLabels }) }}</span>
      </label>
    </div>
    <div
      v-if="regions?.length"
      class="flex flex-wrap gap-2"
      role="group"
      :aria-label="eventsText('scope', locale)"
      data-test="region-chips"
    >
      <button
        v-for="chip in [
          { key: '', label: eventsText('all', locale) },
          ...regions.map((item) => ({ key: item.key, label: pickText(item.label, locale) || item.key })),
        ]"
        :key="chip.key"
        type="button"
        class="rounded-full border px-3 py-1 text-sm"
        :class="region === chip.key ? 'border-brand bg-brand text-brand-foreground' : 'border-border bg-background'"
        :aria-pressed="region === chip.key"
        :data-region="chip.key"
        @click="region = chip.key"
      >
        {{ chip.label }}
      </button>
    </div>

    <FailureNotice v-if="calendarFailure" :failure="calendarFailure" :locale="locale" />
    <p v-else-if="downloadFailed" class="text-sm text-destructive" role="alert">
      {{ eventsText('downloadFailed', locale) }}
    </p>
    <p v-if="trackNotice" class="text-sm text-warning" role="status" data-test="track-changed">
      {{ eventsText('trackChanged', locale) }}
    </p>
    <FailureNotice v-if="trackRequest.failure.value" :failure="trackRequest.failure.value" :locale="locale" />

    <div class="min-h-40 space-y-6" :aria-busy="list.pending.value">
      <div v-if="list.pending.value && !items.length" class="space-y-3">
        <span role="status" class="sr-only">{{ eventsText('loadingEvents', locale) }}</span>
        <div class="space-y-3" aria-hidden="true">
          <SkeletonState v-for="index in 3" :key="index" class="h-24 w-full" />
        </div>
      </div>
      <FailureNotice
        v-else-if="list.failure.value"
        :failure="list.failure.value"
        :locale="locale"
        retry
        @retry="load()"
      />
      <template v-else>
        <section
          v-for="group in groups"
          :key="group.key"
          class="space-y-3"
          :aria-labelledby="`events-group-${group.key}`"
          data-test="event-group"
        >
          <h3
            :id="`events-group-${group.key}`"
            class="text-sm font-semibold uppercase tracking-wide text-muted-foreground"
          >
            {{ eventGroupHeading(group.key, locale) }}
          </h3>
          <ul
            v-if="variant === 'cards'"
            class="grid grid-cols-[repeat(auto-fill,minmax(min(100%,17rem),1fr))] gap-3"
            data-test="event-cards"
          >
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
                :can-track="tracking && canTrack"
                :track-pending="trackRequest.pending.value"
                :viewer-zone="viewerZone"
                @open="(id) => emit('open', id)"
                @track="track"
              />
            </li>
          </ul>
          <ul v-else class="grid gap-3">
            <li v-for="item in group.items" :key="item.event.id" class="min-w-0">
              <EventCard
                :event="item.event"
                :locale="locale"
                :today="day"
                :kinds="kinds"
                :href="eventHref?.(item.event.id)"
                :tracking="item.tracking"
                :stages="stages"
                :default-stage="defaultStage"
                :quick-stages="quickStages"
                :can-track="tracking && canTrack"
                :track-pending="trackRequest.pending.value"
                :regions="regions"
                :shown-fields="tracking ? shownFields : []"
                :viewer-zone="viewerZone"
                @open="(id) => emit('open', id)"
                @track="track"
              />
            </li>
          </ul>
        </section>
        <p v-if="!groups.length && !list.pending.value" class="py-8 text-center text-sm text-muted-foreground">
          {{ filtered ? eventsText('noEventsMatch', locale) : eventsText('noUpcoming', locale) }}
        </p>
        <Button v-if="nextCursor" variant="outline" :is-loading="list.pending.value" @click="load(nextCursor)">
          {{ eventsText('loadMore', locale) }}
        </Button>
      </template>
    </div>
  </section>
</template>
