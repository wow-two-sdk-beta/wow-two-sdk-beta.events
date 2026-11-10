<script setup lang="ts">
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
import { eventTitle, formatEventPlace, safeHttpUrl } from '../facts/EventFactsText';
import { EventsLabels } from '../text/EventsLabels';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';

/**
 * Shows one event in a list: its curated image, title, organizer, place, honest dates, kind and status, the nearest
 * open deadline and, when given, the organization's tracking. Sourced media never reaches it — only `facts.images`.
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
  }>(),
  { locale: 'en', today: undefined, kinds: undefined, href: undefined, tracking: null, stages: undefined },
);

const emit = defineEmits<{ open: [id: string] }>();

const day = computed(() => props.today ?? localIsoDate());
const title = computed(() => eventTitle(props.event.facts, props.locale));
const titleId = computed(() => `event-card-${props.event.id}`);
const place = computed(() => formatEventPlace(props.event.facts, props.locale, { venue: false }));
const dates = computed(() => formatEventDates(props.event, props.locale));
const image = computed(() => {
  const curated = (props.event.facts.images ?? []).find((item) => safeHttpUrl(item.url));
  return curated ? { url: safeHttpUrl(curated.url) ?? '', attribution: curated.attribution ?? '' } : null;
});
const kind = computed(() => EventsLabels.kind(props.event.facts.kind, props.kinds, props.locale));
const status = computed(() => props.event.facts.status ?? 'scheduled');
const deadline = computed(() => nextDeadline(props.event.facts.deadlines, day.value));
const deadlineDays = computed(() => (deadline.value ? deadlineDaysLeft(deadline.value, day.value) : null));

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
          <h3 :id="titleId" class="break-words text-base font-semibold leading-6">
            <a v-if="href" :href="href" class="underline-offset-4 hover:underline" @click="follow">{{ title }}</a>
            <button
              v-else
              type="button"
              class="text-left underline-offset-4 hover:underline"
              @click="emit('open', event.id)"
            >
              {{ title }}
            </button>
          </h3>
          <p v-if="event.facts.organizer?.name" class="break-words text-sm text-muted-foreground">
            {{ event.facts.organizer.name }}
          </p>
          <p v-if="place" class="text-sm text-muted-foreground">{{ place }}</p>
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
        <Badge
          v-if="status !== 'scheduled'"
          size="sm"
          :variant="status === 'cancelled' ? BadgeVariant.Danger : BadgeVariant.Info"
        >
          {{ EventsLabels.status(status, locale) }}
        </Badge>
        <Badge v-if="tracking" size="sm" :variant="BadgeVariant.Brand">
          {{ EventsLabels.stage(tracking.stage, stages, locale) }}
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
    </div>
  </article>
</template>
