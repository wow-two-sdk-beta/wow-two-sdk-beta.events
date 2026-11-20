<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { computed, shallowRef } from 'vue';
import type { EventModel, EventSourceModel, EventSourceState } from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import type { EventsFailure } from '../../kit/client/EventsFailure';
import { formatTimestamp } from '../../kit/dates/EventDates';
import { safeHttpUrl } from '../../kit/facts/EventFactsText';
import { sourceLabel } from '../../kit/facts/EventSources';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import { EventsLabels } from '../../kit/text/EventsLabels';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';

/**
 * Lists the sources behind an event that holds more than one, and splits one off for a reviewer: a source another source's
 * event took in by the title, dates and city rule is marked, and "Split source" moves it to a new candidate of its own and
 * keeps it from attaching to this event again. Two steps: the button asks for a confirmation. An import's source stays.
 */
defineOptions({ name: 'EventSourcesPanel' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    event: EventModel;
    locale?: EventsLocale;
  }>(),
  { locale: 'en' },
);

const emit = defineEmits<{
  /** Fires after a split landed, with the new candidate; the host reloads. */
  split: [candidate: EventModel];
  /** Fires when the event changed elsewhere (409, 404) and the host should reload it. */
  stale: [];
}>();

const confirming = shallowRef<string | null>(null);
const pending = shallowRef(false);
const failure = shallowRef<EventsFailure | null>(null);

const key = (source: EventSourceModel) => `${source.kind}/${source.id}`;
const rows = computed(() =>
  (props.event.sources ?? []).map((source) => ({
    source,
    key: key(source),
    label: sourceLabel(source) ?? eventsText('unnamedSource', props.locale),
    url: safeHttpUrl(source.url),
  })),
);
const canSplit = computed(
  () => (props.event.review === 'candidate' || props.event.review === 'published') && rows.value.length > 1,
);
const stateVariant = (state: EventSourceState) =>
  state === 'gone'
    ? BadgeVariant.Danger
    : state === 'suspect' || state === 'unmatched'
      ? BadgeVariant.Warning
      : BadgeVariant.Neutral;

async function split(source: EventSourceModel) {
  if (confirming.value !== key(source)) {
    confirming.value = key(source);
    return;
  }
  pending.value = true;
  failure.value = null;
  const result = await props.client.splitSource(props.catalog, props.event.id, {
    kind: source.kind,
    sourceId: source.id,
    expectedRevision: props.event.revision,
  });
  pending.value = false;
  confirming.value = null;
  if (result.ok) {
    emit('split', result.value);
    return;
  }
  if (result.failure.reason === 'cancelled') return;
  failure.value = result.failure;
  if (result.failure.reason === 'conflict' || result.failure.reason === 'notFound') emit('stale');
}
</script>

<template>
  <section
    v-if="rows.length > 1"
    class="space-y-2"
    aria-labelledby="event-sources-panel-title"
    data-test="event-sources"
  >
    <h4 id="event-sources-panel-title" class="text-sm font-medium">{{ eventsText('sources', locale) }}</h4>
    <FailureNotice v-if="failure" :failure="failure" :locale="locale" context="review" />
    <ul class="space-y-2 text-sm">
      <li v-for="row in rows" :key="row.key" class="space-y-1" data-test="event-source-row">
        <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
          <a v-if="row.url" :href="row.url" target="_blank" rel="noopener noreferrer" class="font-medium underline">
            {{ row.label }}
          </a>
          <span v-else class="font-medium">{{ row.label }}</span>
          <Badge v-if="row.source.state" :variant="stateVariant(row.source.state)">
            {{ EventsLabels.sourceState(row.source.state, locale) }}
          </Badge>
          <Badge v-if="row.source.matchedBy" :variant="BadgeVariant.Info" data-test="source-matched">
            {{ eventsText('matchedByRule', locale) }}
          </Badge>
          <span class="text-xs text-muted-foreground">
            {{ eventsText('firstSeen', locale) }} {{ formatTimestamp(row.source.firstSeenAtUtc, locale) }}
          </span>
          <Button
            v-if="canSplit && row.source.kind !== 'import'"
            size="sm"
            variant="ghost"
            tone="danger"
            :is-loading="pending && confirming === row.key"
            :is-disabled="pending"
            @click="split(row.source)"
          >
            {{ confirming === row.key ? eventsText('confirmSplitSource', locale) : eventsText('splitSource', locale) }}
          </Button>
        </div>
        <p v-if="confirming === row.key" class="text-xs text-muted-foreground">
          {{ eventsText('splitSourceNote', locale) }}
        </p>
      </li>
    </ul>
  </section>
</template>
