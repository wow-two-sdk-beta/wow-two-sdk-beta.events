<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { onMounted, onScopeDispose, shallowRef, watch } from 'vue';
import {
  EventReviewStateValues,
  type CatalogKindModel,
  type EventModel,
  type EventPage,
  type EventReviewState,
} from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import type { EventsFailure } from '../../kit/client/EventsFailure';
import { formatEventDates, formatTimestamp } from '../../kit/dates/EventDates';
import { eventTitle, formatEventPlace } from '../../kit/facts/EventFactsText';
import { useEventsRequest } from '../../kit/hooks/UseEventsRequest';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import { EventsLabels } from '../../kit/text/EventsLabels';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';

/**
 * Lists every event of a catalog the caller owns, in every review state, with its dates, place, pending changes and a
 * two-step archive. Opening a row emits `open`.
 */
defineOptions({ name: 'CatalogEventsTable' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    locale?: EventsLocale;
    kinds?: readonly CatalogKindModel[];
    pageSize?: number;
  }>(),
  { locale: 'en', kinds: undefined, pageSize: 50 },
);

const emit = defineEmits<{ open: [id: string]; archived: [event: EventModel] }>();

const review = shallowRef<EventReviewState | ''>('');
const search = shallowRef('');
const text = shallowRef('');
const items = shallowRef<readonly EventModel[]>([]);
const nextCursor = shallowRef<string | null>(null);
const confirming = shallowRef<string | null>(null);
const archiving = shallowRef<string | null>(null);
const archiveFailure = shallowRef<EventsFailure | null>(null);
let timer: ReturnType<typeof setTimeout> | undefined;

const list = useEventsRequest((signal, cursor: string | null) =>
  props.client.listEvents(
    props.catalog,
    {
      limit: props.pageSize,
      includeUndated: true,
      ...(review.value ? { review: review.value } : {}),
      ...(text.value ? { text: text.value } : {}),
      ...(cursor ? { cursor } : {}),
    },
    signal,
  ),
);

async function load(cursor: string | null = null) {
  const page: EventPage | null = await list.run(cursor);
  if (!page) return;
  items.value = cursor ? [...items.value, ...page.items] : page.items;
  nextCursor.value = page.nextCursor ?? null;
}

async function archive(event: EventModel) {
  if (confirming.value !== event.id) {
    confirming.value = event.id;
    return;
  }
  archiving.value = event.id;
  const result = await props.client.archiveEvent(props.catalog, event.id, event.revision);
  archiving.value = null;
  confirming.value = null;
  archiveFailure.value = result.ok ? null : result.failure;
  if (result.ok) {
    items.value = items.value.map((item) => (item.id === event.id ? result.value : item));
    emit('archived', result.value);
  } else if (result.failure.reason === 'conflict') void load();
}

watch(search, (value) => {
  clearTimeout(timer);
  timer = setTimeout(() => {
    text.value = value.trim();
  }, 250);
});
watch([review, text, () => props.catalog, () => props.client], () => {
  items.value = [];
  void load();
});
onMounted(() => load());
onScopeDispose(() => clearTimeout(timer));

const asReview = (value: string) => value as EventReviewState | '';
const reviewVariant = (state: EventReviewState) =>
  state === 'published'
    ? BadgeVariant.Success
    : state === 'candidate'
      ? BadgeVariant.Warning
      : state === 'rejected'
        ? BadgeVariant.Danger
        : BadgeVariant.Neutral;
</script>

<template>
  <section class="space-y-4" aria-labelledby="catalog-events-title">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <h2 id="catalog-events-title" class="text-lg font-semibold">{{ eventsText('catalogEvents', locale) }}</h2>
      <div class="flex flex-wrap items-end gap-3">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-muted-foreground">{{ eventsText('searchEvents', locale) }}</span>
          <input
            v-model="search"
            name="search"
            type="search"
            class="rounded-md border border-border bg-background px-3 py-2 text-sm"
            autocomplete="off"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-muted-foreground">{{ eventsText('reviewState', locale) }}</span>
          <select
            name="review"
            class="rounded-md border border-border bg-background px-3 py-2 text-sm"
            :value="review"
            @change="review = asReview(($event.target as HTMLSelectElement).value)"
          >
            <option value="">{{ eventsText('allStates', locale) }}</option>
            <option v-for="item in EventReviewStateValues" :key="item" :value="item">
              {{ EventsLabels.review(item, locale) }}
            </option>
          </select>
        </label>
      </div>
    </div>
    <FailureNotice v-if="archiveFailure" :failure="archiveFailure" :locale="locale" context="review" />
    <div :aria-busy="list.pending.value">
      <div v-if="list.pending.value && !items.length" class="space-y-2" aria-hidden="true">
        <SkeletonState v-for="index in 5" :key="index" class="h-10 w-full" />
      </div>
      <FailureNotice
        v-else-if="list.failure.value"
        :failure="list.failure.value"
        :locale="locale"
        context="review"
        retry
        @retry="load()"
      />
      <p v-else-if="!items.length" class="py-8 text-center text-sm text-muted-foreground">
        {{ eventsText('noEvents', locale) }}
      </p>
      <div v-else class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-border text-left text-xs text-muted-foreground">
              <th class="py-2 pr-3 font-medium">{{ eventsText('title', locale) }}</th>
              <th class="py-2 pr-3 font-medium">{{ eventsText('dates', locale) }}</th>
              <th class="py-2 pr-3 font-medium">{{ eventsText('place', locale) }}</th>
              <th class="py-2 pr-3 font-medium">{{ eventsText('kind', locale) }}</th>
              <th class="py-2 pr-3 font-medium">{{ eventsText('reviewState', locale) }}</th>
              <th class="py-2 pr-3 font-medium">{{ eventsText('updated', locale) }}</th>
              <th class="py-2"></th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in items"
              :key="item.id"
              class="border-b border-border align-top"
              data-test="catalog-event-row"
            >
              <td class="py-2 pr-3">
                <button
                  type="button"
                  class="text-left font-medium underline-offset-4 hover:underline"
                  @click="emit('open', item.id)"
                >
                  {{ eventTitle(item.facts, locale) }}
                </button>
              </td>
              <td class="py-2 pr-3">{{ formatEventDates(item, locale) }}</td>
              <td class="py-2 pr-3">{{ formatEventPlace(item.facts, locale, { venue: false }) }}</td>
              <td class="py-2 pr-3">{{ EventsLabels.kind(item.facts.kind, kinds, locale) }}</td>
              <td class="space-x-1 py-2 pr-3">
                <Badge size="sm" :variant="reviewVariant(item.review)">
                  {{
                    EventsLabels.review(item.review, locale)
                  }}
                </Badge>
                <Badge v-if="item.pendingChanges" size="sm" :variant="BadgeVariant.Info">
                  {{ eventsText('pendingChanges', locale, { count: item.pendingChanges }) }}
                </Badge>
              </td>
              <td class="py-2 pr-3 text-xs text-muted-foreground">{{ formatTimestamp(item.updatedAtUtc, locale) }}</td>
              <td class="py-2 text-right">
                <Button
                  v-if="item.review !== 'archived'"
                  size="sm"
                  variant="ghost"
                  tone="danger"
                  :is-loading="archiving === item.id"
                  @click="archive(item)"
                >
                  {{ confirming === item.id ? eventsText('confirmArchive', locale) : eventsText('archive', locale) }}
                </Button>
              </td>
            </tr>
          </tbody>
        </table>
        <Button
          v-if="nextCursor"
          class="mt-3"
          variant="outline"
          :is-loading="list.pending.value"
          @click="load(nextCursor)"
        >
          {{ eventsText('loadMore', locale) }}
        </Button>
      </div>
    </div>
  </section>
</template>
