<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { onMounted, shallowRef, watch } from 'vue';
import type { CatalogKindModel, ReviewItemModel, ReviewPage } from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import { useEventsRequest } from '../../kit/hooks/UseEventsRequest';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../../kit/text/EventsMessages';
import ReviewItem from './ReviewItem.vue';

/**
 * The reviewer's queue: candidates and published events with pending source changes, oldest first, each with its
 * evidence and decisions. Needs `events:review` in the catalog owner's organization; a 401 or 403 says so.
 */
defineOptions({ name: 'ReviewQueue' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    locale?: EventsLocale;
    kinds?: readonly CatalogKindModel[];
    pageSize?: number;
  }>(),
  { locale: 'en', kinds: undefined, pageSize: 20 },
);

const items = shallowRef<readonly ReviewItemModel[]>([]);
const nextCursor = shallowRef<string | null>(null);
const notice = shallowRef<EventsMessageKey | null>(null);
const list = useEventsRequest((signal, cursor: string | null) =>
  props.client.listReview(props.catalog, { limit: props.pageSize, ...(cursor ? { cursor } : {}) }, signal),
);

async function load(cursor: string | null = null) {
  const page: ReviewPage | null = await list.run(cursor);
  if (!page) return;
  items.value = cursor ? [...items.value, ...page.items] : page.items;
  nextCursor.value = page.nextCursor ?? null;
}

function resolved(message: EventsMessageKey) {
  notice.value = message;
  void load();
}

onMounted(() => load());
watch([() => props.catalog, () => props.client], () => {
  items.value = [];
  notice.value = null;
  void load();
});
</script>

<template>
  <section class="space-y-4" aria-labelledby="review-queue-title">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 id="review-queue-title" class="text-lg font-semibold">{{ eventsText('reviewQueue', locale) }}</h2>
      <Button size="sm" variant="ghost" :is-loading="list.pending.value" @click="load()">
        {{ eventsText('refresh', locale) }}
      </Button>
    </div>
    <p v-if="notice" class="rounded-md bg-success-soft px-3 py-2 text-sm text-success-soft-foreground" role="status">
      {{ eventsText(notice, locale) }}
    </p>
    <div class="space-y-4" :aria-busy="list.pending.value">
      <div v-if="list.pending.value && !items.length" class="space-y-3">
        <span role="status" class="sr-only">{{ eventsText('loading', locale) }}</span>
        <div class="space-y-3" aria-hidden="true">
          <SkeletonState v-for="index in 3" :key="index" class="h-32 w-full" />
        </div>
      </div>
      <FailureNotice
        v-else-if="list.failure.value"
        :failure="list.failure.value"
        :locale="locale"
        context="review"
        retry
        @retry="load()"
      />
      <template v-else>
        <ReviewItem
          v-for="item in items"
          :key="`${item.event.id}-${item.event.revision}`"
          :client="client"
          :catalog="catalog"
          :item="item"
          :locale="locale"
          :kinds="kinds"
          @resolved="resolved"
          @stale="load()"
        />
        <p v-if="!items.length && !list.pending.value" class="py-8 text-center text-sm text-muted-foreground">
          {{ eventsText('nothingToReview', locale) }}
        </p>
        <Button v-if="nextCursor" variant="outline" :is-loading="list.pending.value" @click="load(nextCursor)">
          {{ eventsText('loadMore', locale) }}
        </Button>
      </template>
    </div>
  </section>
</template>
