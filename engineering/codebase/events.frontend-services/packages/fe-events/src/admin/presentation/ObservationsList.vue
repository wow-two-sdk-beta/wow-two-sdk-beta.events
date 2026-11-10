<script setup lang="ts">
import { Button, RefreshButton } from '@wow-two-beta/fe/presentation/actions';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { onMounted, shallowRef, watch } from 'vue';
import type { ObservationModel, ObservationPage, ObservationState } from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import { useEventsRequest } from '../../kit/hooks/UseEventsRequest';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import { EventsLabels } from '../../kit/text/EventsLabels';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';
import ObservationEvidence from './ObservationEvidence.vue';

/**
 * Lists a catalog's observations with their evidence, filtered by what intake did with them; quarantined records — the
 * malformed ones intake stored and never applied — have their own filter, first after all.
 */
defineOptions({ name: 'ObservationsList' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    locale?: EventsLocale;
    /** The state shown first; all states when empty. */
    initialState?: ObservationState | '';
    /** Narrows the list to one event's observations. */
    eventId?: string;
    pageSize?: number;
  }>(),
  { locale: 'en', initialState: '', eventId: undefined, pageSize: 50 },
);

/** Orders the filters: everything, then quarantined, then the rest of the contract's states. */
const filters: readonly (ObservationState | '')[] = [
  '',
  'quarantined',
  'pending',
  'recorded',
  'applied',
  'accepted',
  'dismissed',
  'superseded',
  'ignored',
];

const state = shallowRef<ObservationState | ''>(props.initialState);
const items = shallowRef<readonly ObservationModel[]>([]);
const nextCursor = shallowRef<string | null>(null);
const list = useEventsRequest((signal, cursor: string | null) =>
  props.client.listObservations(
    props.catalog,
    {
      limit: props.pageSize,
      ...(state.value ? { state: state.value } : {}),
      ...(props.eventId ? { eventId: props.eventId } : {}),
      ...(cursor ? { cursor } : {}),
    },
    signal,
  ),
);

async function load(cursor: string | null = null) {
  const page: ObservationPage | null = await list.run(cursor);
  if (!page) return;
  items.value = cursor ? [...items.value, ...page.items] : page.items;
  nextCursor.value = page.nextCursor ?? null;
}

onMounted(() => load());
watch([state, () => props.catalog, () => props.eventId, () => props.client], () => {
  items.value = [];
  void load();
});
</script>

<template>
  <section class="space-y-4" aria-labelledby="observations-title">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 id="observations-title" class="text-lg font-semibold">{{ eventsText('observations', locale) }}</h2>
      <RefreshButton
        variant="ghost"
        :is-refreshing="list.pending.value"
        :label="eventsText('refresh', locale)"
        @click="load()"
      />
    </div>
    <div class="flex flex-wrap gap-2" role="group" :aria-label="eventsText('state', locale)">
      <button
        v-for="filter in filters"
        :key="filter || 'all'"
        type="button"
        class="rounded-full border px-3 py-1 text-xs"
        :class="[
          state === filter ? 'border-primary bg-primary-soft text-primary-soft-foreground' : 'border-border',
          filter === 'quarantined' && state !== filter ? 'border-destructive/50 text-destructive' : '',
        ]"
        :aria-pressed="state === filter ? 'true' : 'false'"
        :data-filter="filter || 'all'"
        @click="state = filter"
      >
        {{ filter ? EventsLabels.observation(filter, locale) : eventsText('all', locale) }}
      </button>
    </div>
    <div class="space-y-3" :aria-busy="list.pending.value">
      <div v-if="list.pending.value && !items.length" class="space-y-3" aria-hidden="true">
        <SkeletonState v-for="index in 3" :key="index" class="h-24 w-full" />
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
        <ObservationEvidence v-for="item in items" :key="item.id" :observation="item" :locale="locale" />
        <p v-if="!items.length && !list.pending.value" class="py-8 text-center text-sm text-muted-foreground">
          {{ eventsText('noObservations', locale) }}
        </p>
        <Button v-if="nextCursor" variant="outline" :is-loading="list.pending.value" @click="load(nextCursor)">
          {{ eventsText('loadMore', locale) }}
        </Button>
      </template>
    </div>
  </section>
</template>
