<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import {
  SelectPicker,
  SelectPickerContent,
  SelectPickerItem,
  SelectPickerTrigger,
  SelectPickerValue,
} from '@wow-two-beta/fe/presentation/forms';
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
import ReviewAttribution from './ReviewAttribution.vue';
import { EventsLabels } from '../../kit/text/EventsLabels';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';

/**
 * Lists every event of a catalog the caller owns, in every review state, with its dates, place, pending changes and a
 * two-step archive. Opening a row emits `open` with its id and the event as listed.
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

const emit = defineEmits<{ open: [id: string, event: EventModel]; archived: [event: EventModel] }>();

const review = shallowRef<EventReviewState | ''>('');
const search = shallowRef('');
const text = shallowRef('');
const items = shallowRef<readonly EventModel[]>([]);
const nextCursor = shallowRef<string | null>(null);
const confirming = shallowRef<string | null>(null);
const archiving = shallowRef<string | null>(null);
const reopening = shallowRef<string | null>(null);
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

async function reopen(event: EventModel) {
  if (!globalThis.confirm(eventsText('confirmReturnToReview', props.locale))) return;
  reopening.value = event.id;
  const result = await props.client.reopenEvent(props.catalog, event.id, event.revision);
  reopening.value = null;
  archiveFailure.value = result.ok ? null : result.failure;
  if (result.ok) {
    items.value = items.value.map((item) => (item.id === event.id ? result.value : item));
    void load();
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
        <div class="flex flex-col gap-1 text-sm">
          <span class="text-muted-foreground">{{ eventsText('reviewState', locale) }}</span>
          <SelectPicker :model-value="review" name="review" @update:model-value="(key) => (review = key ?? '')">
            <SelectPickerTrigger
              width="11rem"
              v-bind="{ 'aria-label': eventsText('reviewState', locale) }"
              data-test="events-review"
            >
              <SelectPickerValue />
            </SelectPickerTrigger>
            <SelectPickerContent>
              <SelectPickerItem item-key="" :label="eventsText('allStates', locale)" />
              <SelectPickerItem
                v-for="item in EventReviewStateValues"
                :key="item"
                :item-key="item"
                :label="EventsLabels.review(item, locale)"
              />
            </SelectPickerContent>
          </SelectPicker>
        </div>
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
                  @click="emit('open', item.id, item)"
                >
                  {{ eventTitle(item.facts, locale) }}
                </button>
              </td>
              <td class="py-2 pr-3">{{ formatEventDates(item, locale) }}</td>
              <td class="py-2 pr-3">{{ formatEventPlace(item.facts, locale, { venue: false }) }}</td>
              <td class="py-2 pr-3">{{ EventsLabels.kind(item.facts.kind, kinds, locale) }}</td>
              <td class="space-x-1 py-2 pr-3">
                <Badge :variant="reviewVariant(item.review)">
                  {{ EventsLabels.review(item.review, locale) }}
                </Badge>
                <Badge v-if="item.pendingChanges" :variant="BadgeVariant.Info">
                  {{ eventsText('pendingChanges', locale, { count: item.pendingChanges }) }}
                </Badge>
                <ReviewAttribution :event="item" :locale="locale" />
              </td>
              <td class="py-2 pr-3 text-xs text-muted-foreground">{{ formatTimestamp(item.updatedAtUtc, locale) }}</td>
              <td class="py-2 text-right">
                <Button
                  v-if="item.review === 'published'"
                  size="sm"
                  variant="ghost"
                  :is-loading="reopening === item.id"
                  @click="reopen(item)"
                >
                  {{ eventsText('returnToReview', locale) }}
                </Button>
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
