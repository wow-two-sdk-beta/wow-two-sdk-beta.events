<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { TextInput } from '@wow-two-beta/fe/presentation/forms';
import { computed, onMounted, onScopeDispose, shallowRef, watch } from 'vue';
import type { CatalogKindModel, EventModel, EventPage, EventQuery } from '../../contracts';
import { downloadEventsCalendar } from '../calendar/DownloadEventsCalendar';
import type { EventsClient } from '../client/CreateEventsClient';
import type { EventsFailure } from '../client/EventsFailure';
import { eventGroupHeading, groupEventsByMonth, localIsoDate } from '../dates/EventDates';
import { eventTitle } from '../facts/EventFactsText';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import EventCard from './EventCard.vue';
import EventProposalForm from './EventProposalForm.vue';
import FailureNotice from './FailureNotice.vue';

/**
 * Lists a catalog's published events grouped by the month they start in, undated last, with search, kind and country
 * filters, past events on request, a calendar download, and — for members — a proposal that waits for review.
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
  }>(),
  { locale: 'en', today: undefined, kinds: undefined, pageSize: 50, canPropose: false, eventHref: undefined },
);

const emit = defineEmits<{
  open: [id: string];
  /** Fires with the candidate a proposal created; it is not in the feed until a reviewer publishes it. */
  proposed: [event: EventModel];
}>();

const day = computed(() => props.today ?? localIsoDate());
const search = shallowRef('');
const text = shallowRef('');
const kind = shallowRef('');
const country = shallowRef('');
const showPast = shallowRef(false);
const items = shallowRef<readonly EventModel[]>([]);
const nextCursor = shallowRef<string | null>(null);
const proposing = shallowRef(false);
const proposedTitle = shallowRef<string | null>(null);
const calendarFailure = shallowRef<EventsFailure | null>(null);
const calendarPending = shallowRef(false);
const downloadFailed = shallowRef(false);
let timer: ReturnType<typeof setTimeout> | undefined;

const filter = computed<EventQuery>(() => ({
  review: 'published',
  includeUndated: true,
  ...(text.value ? { text: text.value } : {}),
  ...(kind.value ? { kind: kind.value } : {}),
  ...(country.value.trim().length === 2 ? { country: country.value.trim().toUpperCase() } : {}),
  ...(showPast.value ? {} : { from: day.value }),
}));
const filtered = computed(() => Boolean(text.value || kind.value || country.value.trim()));

const list = useEventsRequest((signal, cursor: string | null) =>
  props.client.listEvents(
    props.catalog,
    { ...filter.value, limit: props.pageSize, ...(cursor ? { cursor } : {}) },
    signal,
  ),
);

async function load(cursor: string | null = null) {
  const page: EventPage | null = await list.run(cursor);
  if (!page) return;
  items.value = cursor ? [...items.value, ...page.items] : page.items;
  nextCursor.value = page.nextCursor ?? null;
}

const groups = computed(() => groupEventsByMonth(items.value));
const kindOptions = computed(() =>
  (props.kinds ?? []).map((item) => ({ value: item.key, label: pickText(item.label, props.locale) || item.key })),
);

watch(search, (value) => {
  clearTimeout(timer);
  timer = setTimeout(() => {
    text.value = value.trim();
  }, 250);
});
watch([() => JSON.stringify(filter.value), () => props.catalog, () => props.client], () => {
  items.value = [];
  void load();
});
onMounted(() => load());
onScopeDispose(() => clearTimeout(timer));

async function downloadCalendar() {
  calendarPending.value = true;
  const result = await downloadEventsCalendar({ client: props.client, catalog: props.catalog, query: filter.value });
  calendarPending.value = false;
  calendarFailure.value = result.ok ? null : result.failure;
  downloadFailed.value = result.ok && !result.value;
}

function proposed(event: EventModel) {
  proposing.value = false;
  proposedTitle.value = eventTitle(event.facts, props.locale);
  emit('proposed', event);
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
      {{ eventsText('proposed', locale, { title: proposedTitle }) }}
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
      <label v-if="kindOptions.length" class="flex flex-col gap-1 text-sm">
        <span class="text-muted-foreground">{{ eventsText('kind', locale) }}</span>
        <select v-model="kind" name="kind" class="rounded-md border border-border bg-background px-3 py-2 text-sm">
          <option value="">{{ eventsText('allKinds', locale) }}</option>
          <option v-for="item in kindOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
        </select>
      </label>
      <label class="flex w-28 flex-col gap-1 text-sm">
        <span class="text-muted-foreground">{{ eventsText('country', locale) }}</span>
        <TextInput v-model="country" name="country" :maxlength="2" placeholder="UZ" autocomplete="off" />
      </label>
      <label class="flex items-center gap-2 pb-2 text-sm">
        <input v-model="showPast" name="showPast" type="checkbox" />
        <span>{{ eventsText('showPast', locale) }}</span>
      </label>
    </div>

    <FailureNotice v-if="calendarFailure" :failure="calendarFailure" :locale="locale" />
    <p v-else-if="downloadFailed" class="text-sm text-destructive" role="alert">
      {{ eventsText('downloadFailed', locale) }}
    </p>

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
          <ul class="grid gap-3">
            <li v-for="item in group.events" :key="item.id" class="min-w-0">
              <EventCard
                :event="item"
                :locale="locale"
                :today="day"
                :kinds="kinds"
                :href="eventHref?.(item.id)"
                @open="(id) => emit('open', id)"
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
