<script setup lang="ts">
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { computed, onMounted, watch } from 'vue';
import type { UpcomingDeadlineModel, WorkflowStageModel } from '../../contracts';
import type { EventsClient } from '../client/CreateEventsClient';
import { daysLeftLabel, deadlineDaysLeft, deadlineTone, formatEventWhen, localIsoDate } from '../dates/EventDates';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import FailureNotice from './FailureNotice.vue';

/** Lists the organization's nearest open deadlines, soonest first, from its tracking summary. */
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
  }>(),
  { locale: 'en', today: undefined, stages: undefined },
);

const emit = defineEmits<{ open: [eventId: string] }>();

const day = computed(() => props.today ?? localIsoDate());
const summary = useEventsRequest((signal) => props.client.getTrackingSummary(props.catalog, day.value, signal));
const deadlines = computed(() => summary.value.value?.upcomingDeadlines ?? []);
const daysLeft = (item: UpcomingDeadlineModel) => deadlineDaysLeft(item.deadline, day.value);

onMounted(() => summary.run());
watch([() => props.catalog, day, () => props.client], () => summary.run());
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
          <button
            type="button"
            class="font-medium underline-offset-4 hover:underline"
            @click="emit('open', item.eventId)"
          >
            {{ pickText(item.title, locale) }}
          </button>
          <span class="text-muted-foreground">
            {{ EventsLabels.deadline(item.deadline.kind, locale) }}: {{ formatEventWhen(item.deadline.at, locale) }}
          </span>
          <span
            v-if="daysLeft(item) !== null"
            :data-tone="deadlineTone(daysLeft(item) ?? 0)"
            :class="{ 'text-destructive': deadlineTone(daysLeft(item) ?? 0) === 'urgent' }"
          >
            {{ daysLeftLabel(daysLeft(item) ?? 0, locale) }}
          </span>
          <Badge size="sm" :variant="BadgeVariant.Brand">{{ EventsLabels.stage(item.stage, stages, locale) }}</Badge>
        </li>
      </ol>
    </div>
  </section>
</template>
