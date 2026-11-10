<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { computed, shallowRef } from 'vue';
import type { CatalogKindModel, EventFacts, EventModel, ObservationModel, ReviewItemModel } from '../../contracts';
import type { EventsClient, EventsResult } from '../../kit/client/CreateEventsClient';
import type { EventsFailure } from '../../kit/client/EventsFailure';
import { formatEventDates } from '../../kit/dates/EventDates';
import { eventTitle, formatEventPlace } from '../../kit/facts/EventFactsText';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import { EventsLabels } from '../../kit/text/EventsLabels';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../../kit/text/EventsMessages';
import { factChanges } from '../review/FactsChanges';
import ReviewAttribution from './ReviewAttribution.vue';
import ObservationEvidence from './ObservationEvidence.vue';
import ReviewFactsEditor from './ReviewFactsEditor.vue';

/**
 * Reviews one queue entry. A candidate is accepted (as is or with edits), rejected, or merged into a published event; a
 * published event's pending changes are applied (as proposed or with edits) or dismissed. Each observation's evidence
 * sits beside the decision.
 */
defineOptions({ name: 'ReviewItem' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    item: ReviewItemModel;
    locale?: EventsLocale;
    kinds?: readonly CatalogKindModel[];
  }>(),
  { locale: 'en', kinds: undefined },
);

const emit = defineEmits<{
  /** Fires after a decision landed, with the message to show; the queue reloads. */
  resolved: [message: EventsMessageKey];
  /** Fires when the entry changed elsewhere (409) and the queue should reload. */
  stale: [];
}>();

type Mode = 'idle' | 'acceptEdit' | 'merge' | 'confirmReject' | `apply:${string}`;

const mode = shallowRef<Mode>('idle');
const pending = shallowRef(false);
const failure = shallowRef<EventsFailure | null>(null);
const mergeText = shallowRef('');
const mergeResults = shallowRef<readonly EventModel[]>([]);
const mergeTarget = shallowRef<EventModel | null>(null);

const event = computed(() => props.item.event);
const observations = computed(() => props.item.observations ?? []);
const isCandidate = computed(() => event.value.review === 'candidate');
const pendingChanges = computed(() => observations.value.filter((observation) => observation.proposed));
const place = computed(() => formatEventPlace(event.value.facts, props.locale));

async function act<T>(call: EventsResult<T>, message: EventsMessageKey) {
  pending.value = true;
  failure.value = null;
  const result = await call;
  pending.value = false;
  if (result.ok) {
    mode.value = 'idle';
    emit('resolved', message);
    return;
  }
  if (result.failure.reason === 'cancelled') return;
  failure.value = result.failure;
  if (result.failure.reason === 'conflict' || result.failure.reason === 'notFound') emit('stale');
}

const accept = (facts?: EventFacts) =>
  act(
    props.client.acceptEvent(props.catalog, event.value.id, {
      expectedRevision: event.value.revision,
      ...(facts ? { facts } : {}),
    }),
    'accepted',
  );

function reject() {
  if (mode.value !== 'confirmReject') {
    mode.value = 'confirmReject';
    return;
  }
  void act(props.client.rejectEvent(props.catalog, event.value.id, event.value.revision), 'rejected');
}

async function searchMergeTargets() {
  const result = await props.client.listEvents(props.catalog, {
    review: 'published',
    text: mergeText.value.trim() || undefined,
    limit: 10,
  });
  if (result.ok) mergeResults.value = result.value.items.filter((item) => item.id !== event.value.id);
  else failure.value = result.failure;
}

function merge() {
  const target = mergeTarget.value;
  if (!target) return;
  void act(
    props.client.mergeEvent(props.catalog, event.value.id, {
      intoId: target.id,
      expectedRevision: event.value.revision,
      intoExpectedRevision: target.revision,
    }),
    'merged',
  );
}

const apply = (observation: ObservationModel, facts?: EventFacts) =>
  act(
    props.client.applyChange(props.catalog, observation.id, {
      expectedRevision: event.value.revision,
      ...(facts ? { facts } : {}),
    }),
    'applied',
  );
const dismiss = (observation: ObservationModel) =>
  act(props.client.dismissChange(props.catalog, observation.id), 'dismissed');

function openMerge() {
  mode.value = 'merge';
  mergeTarget.value = null;
  void searchMergeTargets();
}
</script>

<template>
  <article
    class="space-y-4 rounded-lg border border-border p-4"
    :aria-labelledby="`review-${event.id}`"
    data-test="review-item"
  >
    <header class="space-y-1">
      <div class="flex flex-wrap items-center gap-2">
        <Badge size="sm" :variant="isCandidate ? BadgeVariant.Warning : BadgeVariant.Info">
          {{
            isCandidate
              ? EventsLabels.review('candidate', locale)
              : eventsText('pendingChanges', locale, { count: pendingChanges.length })
          }}
        </Badge>
        <Badge v-if="event.facts.kind" size="sm" :variant="BadgeVariant.Outline">
          {{ EventsLabels.kind(event.facts.kind, kinds, locale) }}
        </Badge>
        <span class="text-xs text-muted-foreground">r{{ event.revision }}</span>
      </div>
      <h3 :id="`review-${event.id}`" class="break-words text-base font-semibold">
        {{ eventTitle(event.facts, locale) }}
      </h3>
      <p class="text-sm font-medium">{{ formatEventDates(event, locale) }}</p>
      <p v-if="place" class="text-sm text-muted-foreground">{{ place }}</p>
      <ReviewAttribution :event="event" :locale="locale" />
    </header>

    <FailureNotice v-if="failure" :failure="failure" :locale="locale" context="review" />

    <template v-if="isCandidate">
      <div class="flex flex-wrap gap-2">
        <Button size="sm" :is-loading="pending && mode === 'idle'" :is-disabled="pending" @click="accept()">
          {{ eventsText('accept', locale) }}
        </Button>
        <Button size="sm" variant="outline" :is-disabled="pending" @click="mode = 'acceptEdit'">
          {{ eventsText('acceptWithEdits', locale) }}
        </Button>
        <Button size="sm" variant="outline" :is-disabled="pending" @click="openMerge">
          {{ eventsText('mergeInto', locale) }}
        </Button>
        <Button size="sm" variant="ghost" tone="danger" :is-disabled="pending" @click="reject">
          {{ mode === 'confirmReject' ? eventsText('confirmReject', locale) : eventsText('reject', locale) }}
        </Button>
      </div>
      <ReviewFactsEditor
        v-if="mode === 'acceptEdit'"
        :facts="event.facts"
        :submit-label="eventsText('accept', locale)"
        :locale="locale"
        :kinds="kinds"
        :pending="pending"
        @submit="accept"
        @cancel="mode = 'idle'"
      />
      <div v-if="mode === 'merge'" class="space-y-3 rounded-md border border-border p-3" data-test="merge-panel">
        <form class="flex flex-wrap items-end gap-2" @submit.prevent="searchMergeTargets">
          <label class="flex flex-1 flex-col gap-1 text-sm">
            <span class="text-muted-foreground">{{ eventsText('mergeSearch', locale) }}</span>
            <input
              v-model="mergeText"
              name="merge-search"
              type="search"
              class="rounded-md border border-border bg-background px-3 py-2 text-sm"
              autocomplete="off"
            />
          </label>
          <Button type="submit" size="sm" variant="outline">{{ eventsText('refresh', locale) }}</Button>
        </form>
        <fieldset class="space-y-1">
          <label v-for="target in mergeResults" :key="target.id" class="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="merge-target"
              :value="target.id"
              :checked="mergeTarget?.id === target.id"
              @change="mergeTarget = target"
            />
            <span>{{ eventTitle(target.facts, locale) }}</span>
            <span class="text-muted-foreground">· {{ formatEventDates(target, locale) }}</span>
          </label>
        </fieldset>
        <div class="flex gap-2">
          <Button size="sm" :is-disabled="!mergeTarget || pending" :is-loading="pending" @click="merge">
            {{ eventsText('merge', locale) }}
          </Button>
          <Button size="sm" variant="ghost" @click="mode = 'idle'">{{ eventsText('cancel', locale) }}</Button>
        </div>
      </div>
      <div v-if="observations.length" class="space-y-2">
        <h4 class="text-sm font-medium">{{ eventsText('evidence', locale) }}</h4>
        <ObservationEvidence
          v-for="observation in observations"
          :key="observation.id"
          :observation="observation"
          :locale="locale"
        />
      </div>
    </template>

    <template v-else>
      <section
        v-for="observation in observations"
        :key="observation.id"
        class="space-y-3 rounded-md bg-muted/40 p-3"
        data-test="pending-change"
      >
        <h4 class="text-sm font-medium">{{ eventsText('changedFields', locale) }}</h4>
        <table
          v-if="observation.proposed && factChanges(event.facts, observation.proposed, locale).length"
          class="w-full text-sm"
        >
          <thead>
            <tr class="text-left text-xs text-muted-foreground">
              <th class="py-1 pr-3 font-medium"></th>
              <th class="py-1 pr-3 font-medium">{{ eventsText('current', locale) }}</th>
              <th class="py-1 font-medium">
                {{ eventsText('proposedValue', locale) }} ({{ eventsText('sourceEvidence', locale) }})
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="change in factChanges(event.facts, observation.proposed, locale)"
              :key="change.key"
              class="align-top"
            >
              <th class="py-1 pr-3 text-left font-mono text-xs font-normal">{{ change.key }}</th>
              <td class="whitespace-pre-line break-words py-1 pr-3">{{ change.current }}</td>
              <td class="whitespace-pre-line break-words py-1">{{ change.proposed }}</td>
            </tr>
          </tbody>
        </table>
        <p v-else class="text-sm text-muted-foreground">{{ eventsText('noChangedFields', locale) }}</p>
        <div class="flex flex-wrap gap-2">
          <Button size="sm" :is-disabled="pending" @click="apply(observation)">
            {{
              eventsText('apply', locale)
            }}
          </Button>
          <Button
            v-if="observation.proposed"
            size="sm"
            variant="outline"
            :is-disabled="pending"
            @click="mode = `apply:${observation.id}`"
          >
            {{ eventsText('applyWithEdits', locale) }}
          </Button>
          <Button size="sm" variant="ghost" :is-disabled="pending" @click="dismiss(observation)">
            {{ eventsText('dismiss', locale) }}
          </Button>
        </div>
        <ReviewFactsEditor
          v-if="observation.proposed && mode === `apply:${observation.id}`"
          :facts="observation.proposed"
          :submit-label="eventsText('apply', locale)"
          :locale="locale"
          :kinds="kinds"
          :pending="pending"
          @submit="(facts) => apply(observation, facts)"
          @cancel="mode = 'idle'"
        />
        <ObservationEvidence :observation="observation" :locale="locale" :show-proposed="false" />
      </section>
    </template>
  </article>
</template>
