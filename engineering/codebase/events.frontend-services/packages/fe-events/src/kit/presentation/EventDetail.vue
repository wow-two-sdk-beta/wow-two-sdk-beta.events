<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { computed, onMounted, shallowRef, watch } from 'vue';
import { ResultExtensions } from '@wow-two-beta/fe/foundation/results';
import type { CatalogKindModel, EventModel, EventSourceState, TrackingModel } from '../../contracts';
import { createEventCalendar, downloadEventCalendar, type EventCalendarOptions } from '../calendar/EventCalendar';
import type { EventsClient, EventsResult } from '../client/CreateEventsClient';
import {
  daysLeftLabel,
  deadlineDaysLeft,
  deadlineTone,
  formatEventDates,
  formatEventWhen,
  formatTimestamp,
  localIsoDate,
} from '../dates/EventDates';
import {
  eventTitle,
  eventTitleLanguage,
  formatEventPlace,
  formatOfferPrice,
  safeHttpUrl,
} from '../facts/EventFactsText';
import { regionOfCountry, type EventRegion } from '../facts/EventRegions';
import { listingRemoved, sourceLabel } from '../facts/EventSources';
import { shownFieldValue, type TrackingShownField } from '../facts/TrackingFields';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import FailureNotice from './FailureNotice.vue';
import TrackingForm from './TrackingForm.vue';

/**
 * Shows one event with every reviewed fact: curated images, dates as precisely as known, place and scope, deadlines,
 * offers, links and each source by name with its listing state — and, when `trackable`, the organization's chosen
 * tracking fields and its tracking form. Offers the event's own calendar file when its dates are exact. Its images are
 * `facts.images`, each at its `url`.
 */
defineOptions({ name: 'EventDetail' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    eventId: string;
    locale?: EventsLocale;
    /** The person's calendar day, `YYYY-MM-DD`; the browser's by default. */
    today?: string;
    kinds?: readonly CatalogKindModel[];
    /** Whether to show the organization's tracking beside the event. */
    trackable?: boolean;
    /** Whether the tracking form may write (`events:track`). */
    canTrack?: boolean;
    /** The consumer's regions, to show the event's scope from its country. */
    regions?: readonly EventRegion[];
    /** Tracking fields shown read-only beside the facts, when `trackable`. */
    shownFields?: readonly TrackingShownField[];
    /** Tracking field keys the tracking form leaves out (Ocharo: the import's `sourceUrl`). */
    hiddenFields?: readonly string[];
    /** Whether to offer the event's own calendar file. */
    calendar?: boolean;
    /** Shapes the calendar file: its name, `PRODID` and `UID` suffix. */
    calendarOptions?: EventCalendarOptions;
    /** The viewer's IANA zone, to name the start time's zone only when it differs; the browser's by default. */
    viewerZone?: string;
  }>(),
  {
    locale: 'en',
    today: undefined,
    kinds: undefined,
    trackable: false,
    canTrack: true,
    regions: undefined,
    shownFields: () => [],
    hiddenFields: () => [],
    calendar: true,
    calendarOptions: undefined,
    viewerZone: undefined,
  },
);

const emit = defineEmits<{
  /** Fires when the tracking form's draft starts or stops differing from what is stored. */
  dirty: [dirty: boolean];
  /** Fires with the tracking the form saved. */
  trackingSaved: [tracking: TrackingModel];
}>();

const tracking = shallowRef<TrackingModel | null>(null);
const calendarFailed = shallowRef(false);
const request = useEventsRequest(async (signal): EventsResult<EventModel> => {
  if (!props.trackable) return props.client.getEvent(props.catalog, props.eventId, signal);
  const result = await props.client.getTracking(props.catalog, props.eventId, signal);
  if (!result.ok) return result;
  tracking.value = result.value.tracking ?? null;
  return ResultExtensions.ok(result.value.event);
});
const event = computed(() => request.value.value);
const facts = computed(() => event.value?.facts);
const day = computed(() => props.today ?? localIsoDate());
const images = computed(() =>
  (facts.value?.images ?? [])
    .map((image) => ({ ...image, safe: safeHttpUrl(image.url) }))
    .filter((image): image is typeof image & { safe: string } => image.safe !== null),
);
const links = computed(() =>
  [
    { key: 'website' as const, url: safeHttpUrl(facts.value?.website) },
    { key: 'tickets' as const, url: safeHttpUrl(facts.value?.ticketUrl) },
    { key: 'joinOnline' as const, url: safeHttpUrl(facts.value?.onlineUrl) },
  ].filter((link): link is { key: 'website' | 'tickets' | 'joinOnline'; url: string } => link.url !== null),
);
const summary = computed(() => pickText(facts.value?.summary, props.locale));
const place = computed(() => (facts.value ? formatEventPlace(facts.value, props.locale) : ''));
const address = computed(() => facts.value?.place?.address ?? '');
const dates = computed(() =>
  event.value
    ? formatEventDates(event.value, props.locale, props.viewerZone ? { viewerZone: props.viewerZone } : {})
    : '',
);
const titleLanguage = computed(() => (facts.value ? eventTitleLanguage(facts.value, props.locale) : null));
const scope = computed(() => {
  if (!props.regions?.length || !facts.value) return null;
  const region = regionOfCountry(facts.value.place?.country, props.regions);
  return region ? pickText(region.label, props.locale) || region.key : eventsText('scopeUnknown', props.locale);
});
const removed = computed(() => (event.value ? listingRemoved(event.value) : false));
const sources = computed(() =>
  (event.value?.sources ?? []).map((source) => ({
    source,
    label: sourceLabel(source) ?? eventsText('unnamedSource', props.locale),
    url: safeHttpUrl(source.url),
  })),
);
const sourceUpdated = computed(() =>
  event.value && tracking.value && event.value.updatedAtUtc > tracking.value.updatedAtUtc
    ? event.value.updatedAtUtc
    : null,
);
const fields = computed(() =>
  props.trackable
    ? props.shownFields
        .map((field) => ({
          key: field.key,
          label: pickText(field.label, props.locale) || field.key,
          value: shownFieldValue(tracking.value?.fields, field.key),
          empty: field.empty ? pickText(field.empty, props.locale) : '',
        }))
        .filter((field) => field.value !== null || field.empty)
    : [],
);
const calendarFile = computed(() =>
  props.calendar && event.value ? createEventCalendar(event.value, props.locale, props.calendarOptions) : null,
);
const stateVariant = (state: EventSourceState | null | undefined) =>
  state === 'gone'
    ? BadgeVariant.Danger
    : state === 'suspect' || state === 'unmatched'
      ? BadgeVariant.Warning
      : BadgeVariant.Neutral;

function downloadCalendar() {
  calendarFailed.value = calendarFile.value ? !downloadEventCalendar(calendarFile.value) : true;
}

function saved(next: TrackingModel) {
  tracking.value = next;
  emit('trackingSaved', next);
}

onMounted(() => request.run());
watch([() => props.catalog, () => props.eventId, () => props.client, () => props.trackable], () => request.run());
</script>

<template>
  <div class="space-y-4">
    <div v-if="request.pending.value && !event" class="space-y-3" aria-busy="true">
      <span role="status" class="sr-only">{{ eventsText('loadingEvent', locale) }}</span>
      <div class="space-y-3" aria-hidden="true">
        <SkeletonState class="h-8 w-3/5" />
        <SkeletonState class="h-4 w-2/5" />
        <SkeletonState class="h-32 w-full" />
      </div>
    </div>
    <template v-else-if="request.failure.value">
      <h2 class="text-xl font-semibold">{{ eventsText('eventUnavailable', locale) }}</h2>
      <FailureNotice :failure="request.failure.value" :locale="locale" retry @retry="request.run()" />
    </template>
    <article v-else-if="event && facts" class="space-y-5" aria-labelledby="event-detail-title">
      <header class="space-y-2">
        <div class="flex flex-wrap gap-2">
          <Badge v-if="event.review !== 'published'" :variant="BadgeVariant.Warning">
            {{ EventsLabels.review(event.review, locale) }}
          </Badge>
          <Badge v-if="facts.kind" :variant="BadgeVariant.Outline">
            {{ EventsLabels.kind(facts.kind, kinds, locale) }}
          </Badge>
          <Badge
            v-if="facts.status && facts.status !== 'scheduled'"
            :variant="facts.status === 'cancelled' ? BadgeVariant.Danger : BadgeVariant.Info"
          >
            {{ EventsLabels.status(facts.status, locale) }}
          </Badge>
          <Badge v-if="scope" :variant="BadgeVariant.Neutral" data-test="event-scope">{{ scope }}</Badge>
          <Badge v-if="removed" :variant="BadgeVariant.Warning" data-test="event-removed">
            {{ eventsText('listingRemoved', locale) }}
          </Badge>
        </div>
        <h2 id="event-detail-title" class="break-words text-2xl font-semibold">
          <span :class="{ 'line-through': facts.status === 'cancelled' }">{{ eventTitle(facts, locale) }}</span>
          <span
            v-if="titleLanguage"
            class="ml-2 rounded border border-border px-1 align-middle text-xs font-medium text-muted-foreground"
            :title="eventsText('titleLanguage', locale, { language: titleLanguage })"
            data-test="title-language"
            v-text="titleLanguage"
          ></span>
        </h2>
        <p v-if="sourceUpdated" class="text-sm text-info" data-test="event-source-updated">
          {{ eventsText('updatedBySource', locale, { date: formatTimestamp(sourceUpdated, locale) }) }}
        </p>
        <p v-if="facts.organizer?.name" class="text-sm text-muted-foreground">
          <a
            v-if="safeHttpUrl(facts.organizer.website)"
            :href="safeHttpUrl(facts.organizer.website) ?? undefined"
            rel="noopener noreferrer"
            target="_blank"
            class="underline"
          >
            {{ facts.organizer.name }}
          </a>
          <template v-else>{{ facts.organizer.name }}</template>
        </p>
      </header>

      <div v-if="images.length" class="flex gap-3 overflow-x-auto" data-test="event-images">
        <figure v-for="image in images" :key="image.safe" class="shrink-0">
          <img
            :src="image.safe"
            :alt="image.attribution ? eventsText('imageBy', locale, { attribution: image.attribution }) : ''"
            class="h-48 rounded-md object-cover"
            loading="lazy"
          />
          <figcaption v-if="image.attribution" class="mt-1 text-xs text-muted-foreground">
            {{ image.attribution }}
          </figcaption>
        </figure>
      </div>

      <p v-if="summary" class="whitespace-pre-line">{{ summary }}</p>

      <dl class="grid gap-4 sm:grid-cols-2">
        <div>
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ eventsText('when', locale) }}</dt>
          <dd class="font-medium" data-test="event-dates">{{ dates }}</dd>
          <dd v-if="calendarFile">
            <Button size="sm" variant="ghost" data-test="event-calendar" @click="downloadCalendar">
              {{ eventsText('downloadCalendar', locale) }}
            </Button>
            <span v-if="calendarFailed" class="text-sm text-destructive" role="alert">
              {{ eventsText('downloadFailed', locale) }}
            </span>
          </dd>
        </div>
        <div v-if="place || address || facts.attendance">
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ eventsText('where', locale) }}</dt>
          <dd v-if="place">{{ place }}</dd>
          <dd v-if="address" class="text-sm text-muted-foreground">{{ address }}</dd>
          <dd v-if="facts.attendance" class="text-sm text-muted-foreground">
            {{ EventsLabels.attendance(facts.attendance, locale) }}
            <template v-if="facts.audience"> · {{ EventsLabels.audience(facts.audience, locale) }}</template>
          </dd>
        </div>
        <div v-if="facts.languages?.length">
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ eventsText('languages', locale) }}</dt>
          <dd>{{ facts.languages.join(', ') }}</dd>
        </div>
        <div v-for="field in fields" :key="field.key" :data-test="`event-field-${field.key}`">
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ field.label }}</dt>
          <dd class="whitespace-pre-line" :class="{ 'text-muted-foreground': field.value === null }">
            {{ field.value ?? field.empty }}
          </dd>
        </div>
        <div v-if="facts.tags?.length">
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ eventsText('tags', locale) }}</dt>
          <dd class="flex flex-wrap gap-1">
            <Badge v-for="tag in facts.tags" :key="tag" :variant="BadgeVariant.Neutral">{{ tag }}</Badge>
          </dd>
        </div>
      </dl>

      <section v-if="facts.deadlines?.length" class="space-y-2" aria-labelledby="event-deadlines-title">
        <h3 id="event-deadlines-title" class="font-semibold">{{ eventsText('deadlines', locale) }}</h3>
        <ul class="space-y-1 text-sm">
          <li
            v-for="(deadline, index) in facts.deadlines"
            :key="index"
            class="flex flex-wrap items-center gap-x-2"
            data-test="event-deadline"
          >
            <span class="font-medium">{{ deadline.label || EventsLabels.deadline(deadline.kind, locale) }}:</span>
            <span>{{ formatEventWhen(deadline.at, locale) }}</span>
            <span
              v-if="deadlineDaysLeft(deadline, day) !== null"
              :data-tone="deadlineTone(deadlineDaysLeft(deadline, day) ?? 0)"
              class="text-muted-foreground"
            >
              · {{ daysLeftLabel(deadlineDaysLeft(deadline, day) ?? 0, locale) }}
            </span>
            <a
              v-if="safeHttpUrl(deadline.url)"
              :href="safeHttpUrl(deadline.url) ?? undefined"
              target="_blank"
              rel="noopener noreferrer"
              class="underline"
            >
              {{ eventsText('links', locale) }}
            </a>
          </li>
        </ul>
      </section>

      <section v-if="facts.offers?.length" class="space-y-2" aria-labelledby="event-offers-title">
        <h3 id="event-offers-title" class="font-semibold">{{ eventsText('offers', locale) }}</h3>
        <ul class="space-y-1 text-sm">
          <li v-for="(offer, index) in facts.offers" :key="index" class="flex flex-wrap gap-x-2">
            <span class="font-medium">{{ offer.name || EventsLabels.offer(offer.kind, locale) }}:</span>
            <span>{{ formatOfferPrice(offer, locale) }}</span>
            <span v-if="offer.terms" class="text-muted-foreground">· {{ offer.terms }}</span>
            <a
              v-if="safeHttpUrl(offer.url)"
              :href="safeHttpUrl(offer.url) ?? undefined"
              target="_blank"
              rel="noopener noreferrer"
              class="underline"
            >
              {{ eventsText('links', locale) }}
            </a>
          </li>
        </ul>
      </section>

      <section v-if="links.length" class="space-y-2" aria-labelledby="event-links-title">
        <h3 id="event-links-title" class="font-semibold">{{ eventsText('links', locale) }}</h3>
        <ul class="flex flex-wrap gap-3 text-sm">
          <li v-for="link in links" :key="link.key">
            <a :href="link.url" target="_blank" rel="noopener noreferrer" class="underline">{{
              eventsText(link.key, locale)
            }}</a>
          </li>
        </ul>
      </section>

      <section v-if="event.sources?.length" class="space-y-2" aria-labelledby="event-sources-title">
        <h3 id="event-sources-title" class="font-semibold">{{ eventsText('sources', locale) }}</h3>
        <ul class="space-y-1 text-sm">
          <li
            v-for="item in sources"
            :key="`${item.source.kind}-${item.source.id}`"
            class="flex flex-wrap items-center gap-x-2 gap-y-1"
            data-test="event-source"
          >
            <a v-if="item.url" :href="item.url" target="_blank" rel="noopener noreferrer" class="font-medium underline">
              {{ item.label }}
            </a>
            <span v-else class="font-medium">{{ item.label }}</span>
            <Badge v-if="item.source.state" :variant="stateVariant(item.source.state)">
              {{ EventsLabels.sourceState(item.source.state, locale) }}
            </Badge>
            <span class="text-xs text-muted-foreground">
              {{ eventsText('firstSeen', locale) }} {{ formatTimestamp(item.source.firstSeenAtUtc, locale) }} ·
              {{ eventsText('lastSeen', locale) }} {{ formatTimestamp(item.source.lastSeenAtUtc, locale) }}
            </span>
          </li>
        </ul>
      </section>
    </article>

    <TrackingForm
      v-if="trackable && event"
      :client="client"
      :catalog="catalog"
      :event-id="eventId"
      :locale="locale"
      :can-edit="canTrack"
      :hidden-fields="hiddenFields"
      @saved="saved"
      @dirty="(value) => emit('dirty', value)"
    />
  </div>
</template>
