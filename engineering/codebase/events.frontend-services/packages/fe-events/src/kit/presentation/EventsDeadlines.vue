<script setup lang="ts">
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { computed, onMounted, watch } from 'vue';
import type { DeadlineKind, UpcomingDeadlineModel, WorkflowStageModel } from '../../contracts';
import type { EventsClient } from '../client/CreateEventsClient';
import { daysLeftLabel, deadlineDaysLeft, deadlineTone, formatEventWhen, localIsoDate } from '../dates/EventDates';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import FailureNotice from './FailureNotice.vue';

/**
 * Lists the nearest open deadlines of the catalog's events that are not cancelled, soonest first, from the organization's
 * tracking summary: an untracked event's under the workflow's default stage, a lost stage's never (mark a stage `lost`
 * in the workflow to keep its events out). Application deadlines read "Apply by {date}".
 */
defineOptions({ name: 'EventsDeadlines' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    locale?: EventsLocale;
    /** The person's calendar day, `YYYY-MM-DD`; the browser's by default. */
    today?: string;
    /** The organization's workflow stages, to label each deadline's stage. */
    stages?: readonly WorkflowStageModel[];
    /** The deadline kinds to list (Ocharo: `application`); every kind when absent. */
    deadlineKinds?: readonly DeadlineKind[];
    /** Builds an event's page link; each title emits `open` on a plain click either way. */
    eventHref?: (id: string) => string;
  }>(),
  { locale: 'en', today: undefined, stages: undefined, deadlineKinds: undefined, eventHref: undefined },
);

const emit = defineEmits<{ open: [eventId: string] }>();

const day = computed(() => props.today ?? localIsoDate());
const summary = useEventsRequest((signal) =>
  props.client.getTrackingSummary(
    props.catalog,
    props.deadlineKinds?.length ? { today: day.value, deadlineKinds: props.deadlineKinds } : day.value,
    signal,
  ),
);
const deadlines = computed(() => summary.value.value?.upcomingDeadlines ?? []);
const daysLeft = (item: UpcomingDeadlineModel) => deadlineDaysLeft(item.deadline, day.value);
const when = (item: UpcomingDeadlineModel) =>
  item.deadline.kind === 'application'
    ? eventsText('applyBy', props.locale, { date: formatEventWhen(item.deadline.at, props.locale) })
    : `${EventsLabels.deadline(item.deadline.kind, props.locale)}: ${formatEventWhen(item.deadline.at, props.locale)}`;

function follow(event: MouseEvent, id: string) {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  emit('open', id);
}

onMounted(() => summary.run());
watch([() => props.catalog, day, () => props.client, () => JSON.stringify(props.deadlineKinds ?? [])], () =>
  summary.run(),
);
defineExpose({ reload: () => summary.run() });
</script>

<template>
  <section class="space-y-3 rounded-lg border border-border p-4" aria-labelledby="events-deadlines-title">
    <h2 id="events-deadlines-title" class="text-base font-semibold">{{ eventsText('nextDeadlines', locale) }}</h2>
    <div class="min-h-16" :aria-busy="summary.pending.value">
      <div v-if="summary.pending.value && !summary.value.value" class="space-y-2" aria-hidden="true">
        <SkeletonState v-for="index in 3" :key="index" class="h-5 w-full" />
      </div>
      <FailureNotice
        v-else-if="summary.failure.value"
        :failure="summary.failure.value"
        :locale="locale"
        retry
        @retry="summary.run()"
      />
      <p v-else-if="!deadlines.length" class="text-sm text-muted-foreground">{{ eventsText('noDeadlines', locale) }}</p>
      <ol v-else class="space-y-2">
        <li
          v-for="item in deadlines"
          :key="`${item.eventId}-${item.deadline.kind}-${item.deadline.at.value}`"
          class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm"
          data-test="deadline"
        >
          <a
            v-if="eventHref"
            :href="eventHref(item.eventId)"
            class="font-medium underline-offset-4 hover:underline"
            @click="follow($event, item.eventId)"
            v-text="pickText(item.title, locale)"
          ></a>
          <button
            v-else
            type="button"
            class="font-medium underline-offset-4 hover:underline"
            @click="emit('open', item.eventId)"
          >
            {{ pickText(item.title, locale) }}
          </button>
          <span class="text-muted-foreground" data-test="deadline-when">{{ when(item) }}</span>
          <span
            v-if="daysLeft(item) !== null"
            :data-tone="deadlineTone(daysLeft(item) ?? 0)"
            :class="{ 'text-destructive': deadlineTone(daysLeft(item) ?? 0) === 'urgent' }"
          >
            {{ daysLeftLabel(daysLeft(item) ?? 0, locale) }}
          </span>
          <Badge :variant="BadgeVariant.Brand">{{ EventsLabels.stage(item.stage, stages, locale) }}</Badge>
        </li>
      </ol>
    </div>
  </section>
</template>
