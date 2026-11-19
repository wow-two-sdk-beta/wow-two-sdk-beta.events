<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { computed } from 'vue';
import type { CatalogKindModel, EventModel, TrackingModel, WorkflowStageModel } from '../../contracts';
import {
  daysLeftLabel,
  deadlineDaysLeft,
  deadlineTone,
  formatEventDates,
  formatEventWhen,
  localIsoDate,
  nextDeadline,
} from '../dates/EventDates';
import {
  eventTitle,
  eventTitleLanguage,
  formatEventPlace,
  formatOfferPrice,
  safeHttpUrl,
  ticketOffer,
} from '../facts/EventFactsText';
import { regionOfCountry, type EventRegion } from '../facts/EventRegions';
import { attributedSource, listingRemoved } from '../facts/EventSources';
import { shownFieldValue, type TrackingShownField } from '../facts/TrackingFields';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';

/**
 * Shows one event in a list: its curated image, title (with a tag when written in another language), organizer, place,
 * honest dates, kind and status, the nearest open deadline, the source it was found on, and — when given — the
 * organization's tracking: stage, priority, chosen fields and quick stage actions. Sourced media never reaches it — only
 * `facts.images`.
 */
defineOptions({ name: 'EventCard' });

const props = withDefaults(
  defineProps<{
    event: EventModel;
    locale?: EventsLocale;
    /** The person's calendar day, `YYYY-MM-DD`; the browser's by default. */
    today?: string;
    /** The catalog's kinds, to label the event's kind. */
    kinds?: readonly CatalogKindModel[];
    /** The event's own page; the title emits `open` on a plain click either way. */
    href?: string;
    /** The organization's tracking of the event. */
    tracking?: TrackingModel | null;
    /** The organization's workflow stages, to label the tracking's stage. */
    stages?: readonly WorkflowStageModel[];
    /** The workflow's default stage: an untracked event shows it, with a "New — review" marker. */
    defaultStage?: string;
    /** Stages offered as one-click actions on an event at the default stage (Ocharo: shortlisted, skipped). */
    quickStages?: readonly string[];
    /** Whether the person may write tracking; the quick actions show only then. */
    canTrack?: boolean;
    /** Whether a quick action's write is running. */
    trackPending?: boolean;
    /** The consumer's regions, to show the event's scope from its country. */
    regions?: readonly EventRegion[];
    /** Tracking fields shown read-only on the card. */
    shownFields?: readonly TrackingShownField[];
    /** The viewer's IANA zone, to name the start time's zone only when it differs; the browser's by default. */
    viewerZone?: string;
  }>(),
  {
    locale: 'en',
    today: undefined,
    kinds: undefined,
    href: undefined,
    tracking: null,
    stages: undefined,
    defaultStage: undefined,
    quickStages: () => [],
    canTrack: false,
    trackPending: false,
    regions: undefined,
    shownFields: () => [],
    viewerZone: undefined,
  },
);

const emit = defineEmits<{
  open: [id: string];
  /** Asks to move the event to a stage: a quick action on an event at the default stage. */
  track: [eventId: string, stage: string];
}>();

const day = computed(() => props.today ?? localIsoDate());
const title = computed(() => eventTitle(props.event.facts, props.locale));
const titleLanguage = computed(() => eventTitleLanguage(props.event.facts, props.locale));
const titleId = computed(() => `event-card-${props.event.id}`);
const place = computed(() => formatEventPlace(props.event.facts, props.locale, { venue: false }));
const dates = computed(() =>
  formatEventDates(props.event, props.locale, props.viewerZone ? { viewerZone: props.viewerZone } : {}),
);
const image = computed(() => {
  const curated = (props.event.facts.images ?? []).find((item) => safeHttpUrl(item.url));
  return curated ? { url: safeHttpUrl(curated.url) ?? '', attribution: curated.attribution ?? '' } : null;
});
const kind = computed(() => EventsLabels.kind(props.event.facts.kind, props.kinds, props.locale));
const status = computed(() => props.event.facts.status ?? 'scheduled');
const cancelled = computed(() => status.value === 'cancelled');
const deadline = computed(() => nextDeadline(props.event.facts.deadlines, day.value));
const deadlineDays = computed(() => (deadline.value ? deadlineDaysLeft(deadline.value, day.value) : null));
const stage = computed(() => props.tracking?.stage ?? props.defaultStage ?? null);
const untracked = computed(() => !props.tracking && Boolean(props.defaultStage));
const atDefault = computed(() => Boolean(props.defaultStage) && stage.value === props.defaultStage);
const quick = computed(() =>
  props.canTrack && atDefault.value ? props.quickStages.filter((item) => item !== props.defaultStage) : [],
);
const source = computed(() => attributedSource(props.event));
const sourceUrl = computed(() => safeHttpUrl(source.value?.url));
const via = computed(() => {
  const [before = '', after = ''] = eventsText('via', props.locale, { source: '\u0000' }).split('\u0000');
  return { before, after };
});
const external = { target: '_blank', rel: 'noopener noreferrer' } as const;
const titleClass = computed(() => ['underline-offset-4 hover:underline', { 'line-through': cancelled.value }]);
const removed = computed(() => listingRemoved(props.event));
const scope = computed(() => {
  if (!props.regions?.length) return null;
  const region = regionOfCountry(props.event.facts.place?.country, props.regions);
  return region ? pickText(region.label, props.locale) || region.key : eventsText('scopeUnknown', props.locale);
});
const ticket = computed(() => ticketOffer(props.event.facts));
const fields = computed(() =>
  props.shownFields
    .map((field) => {
      const value = shownFieldValue(props.tracking?.fields, field.key);
      const empty = field.empty ? pickText(field.empty, props.locale) : '';
      return { key: field.key, label: pickText(field.label, props.locale) || field.key, value, empty };
    })
    .filter((field) => field.value !== null || field.empty),
);

function follow(event: MouseEvent) {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  emit('open', props.event.id);
}
</script>

<template>
  <article
    class="flex gap-4 rounded-lg border border-border bg-card p-4 text-card-foreground"
    :aria-labelledby="titleId"
    data-test="event-card"
  >
    <img
      v-if="image"
      :src="image.url"
      :alt="image.attribution ? eventsText('imageBy', locale, { attribution: image.attribution }) : ''"
      class="h-20 w-20 shrink-0 rounded-md object-cover"
      loading="lazy"
    />
    <div class="min-w-0 flex-1 space-y-2">
      <div class="flex items-start justify-between gap-3">
        <div class="min-w-0 space-y-1">
          <p v-if="untracked" class="text-xs font-medium text-brand" data-test="event-untracked">
            {{ eventsText('untrackedMarker', locale) }}
          </p>
          <h3 :id="titleId" class="break-words text-base font-semibold leading-6">
            <a v-if="href" :href="href" :class="titleClass" @click="follow" v-text="title"></a>
            <button v-else type="button" :class="['text-left', titleClass]" @click="emit('open', event.id)">
              {{ title }}
            </button>
            <span
              v-if="titleLanguage"
              class="ml-2 rounded border border-border px-1 align-middle text-xs font-medium text-muted-foreground"
              :title="eventsText('titleLanguage', locale, { language: titleLanguage })"
              data-test="title-language"
              v-text="titleLanguage"
            ></span>
          </h3>
          <p v-if="event.facts.organizer?.name" class="break-words text-sm text-muted-foreground">
            {{ event.facts.organizer.name }}
          </p>
          <p v-if="place" class="text-sm text-muted-foreground">{{ place }}</p>
          <p v-if="source?.name" class="text-xs text-muted-foreground" data-test="event-source">
            <span v-if="via.before">{{ via.before }}</span>
            <a v-if="sourceUrl" :href="sourceUrl" v-bind="external" class="underline" v-text="source.name"></a>
            <span v-else>{{ source.name }}</span>
            <span v-if="via.after">{{ via.after }}</span>
          </p>
        </div>
        <Badge v-if="tracking?.priority === 'high'" size="sm" :variant="BadgeVariant.Warning">
          {{ eventsText('highPriority', locale) }}
        </Badge>
      </div>
      <p class="text-sm font-medium" data-test="event-dates">{{ dates }}</p>
      <div class="flex flex-wrap gap-2">
        <Badge v-if="event.review !== 'published'" size="sm" :variant="BadgeVariant.Warning">
          {{ EventsLabels.review(event.review, locale) }}
        </Badge>
        <Badge v-if="kind" size="sm" :variant="BadgeVariant.Outline">{{ kind }}</Badge>
        <Badge v-if="scope" size="sm" :variant="BadgeVariant.Neutral" data-test="event-scope">{{ scope }}</Badge>
        <Badge v-if="status !== 'scheduled'" size="sm" :variant="cancelled ? BadgeVariant.Danger : BadgeVariant.Info">
          {{ EventsLabels.status(status, locale) }}
        </Badge>
        <Badge
          v-if="event.facts.attendance === 'online'"
          size="sm"
          :variant="BadgeVariant.Info"
          data-test="event-online"
        >
          {{ EventsLabels.attendance('online', locale) }}
        </Badge>
        <Badge v-if="removed" size="sm" :variant="BadgeVariant.Warning" data-test="event-removed">
          {{ eventsText('listingRemoved', locale) }}
        </Badge>
        <Badge v-if="stage" size="sm" :variant="BadgeVariant.Brand" data-test="event-stage">
          {{ EventsLabels.stage(stage, stages, locale) }}
        </Badge>
      </div>
      <p v-if="deadline" class="text-sm" data-test="event-deadline">
        <span class="text-muted-foreground">{{ EventsLabels.deadline(deadline.kind, locale) }}:</span>
        {{ formatEventWhen(deadline.at, locale) }}
        <template v-if="deadlineDays !== null">
          ·
          <span
            :data-tone="deadlineTone(deadlineDays)"
            :class="{ 'text-destructive': deadlineTone(deadlineDays) === 'urgent' }"
          >
            {{ daysLeftLabel(deadlineDays, locale) }}
          </span>
        </template>
      </p>
      <p v-if="ticket" class="text-sm" data-test="event-tickets">
        <span class="text-muted-foreground">{{ eventsText('tickets', locale) }}:</span>
        {{ formatOfferPrice(ticket, locale) }}
      </p>
      <p v-for="field in fields" :key="field.key" class="text-sm" :data-test="`event-field-${field.key}`">
        <template v-if="field.value !== null">
          <span class="text-muted-foreground">{{ field.label }}:</span> {{ field.value }}
        </template>
        <span v-else class="text-muted-foreground">{{ field.empty }}</span>
      </p>
      <div v-if="quick.length" class="flex flex-wrap gap-2" data-test="event-quick-stages">
        <Button
          v-for="item in quick"
          :key="item"
          size="sm"
          variant="outline"
          :is-disabled="trackPending"
          @click="emit('track', event.id, item)"
        >
          {{ EventsLabels.stage(item, stages, locale) }}
        </Button>
      </div>
    </div>
  </article>
</template>
