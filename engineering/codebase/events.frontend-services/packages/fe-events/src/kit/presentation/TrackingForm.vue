<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { TextAreaInput } from '@wow-two-beta/fe/presentation/forms';
import { computed, onMounted, shallowRef, watch } from 'vue';
import {
  EventsConstants,
  TrackingPriorityValues,
  type TrackedEventModel,
  type TrackingModel,
  type TrackingPriority,
  type TrackingSpec,
  type WorkflowModel,
} from '../../contracts';
import type { EventsClient } from '../client/CreateEventsClient';
import { formatTimestamp } from '../dates/EventDates';
import {
  trackingFieldDefinitions,
  trackingFieldsPayload,
  trackingFieldValues,
  type TrackingFieldValue,
} from '../facts/TrackingFields';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import { EventsLabels } from '../text/EventsLabels';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import FailureNotice from './FailureNotice.vue';

/**
 * Edits the caller's organization's private tracking of one event: the stage from its workflow, the priority, notes and
 * the workflow's custom fields; shows the stage history. Tracking never changes the event.
 */
defineOptions({ name: 'TrackingForm' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    eventId: string;
    locale?: EventsLocale;
    /** Whether the person may write tracking (`events:track`); read-only otherwise. */
    canEdit?: boolean;
  }>(),
  { locale: 'en', canEdit: true },
);

const emit = defineEmits<{ saved: [tracking: TrackingModel]; removed: [] }>();

const workflow = useEventsRequest((signal) => props.client.getWorkflow(signal));
const tracked = useEventsRequest((signal) => props.client.getTracking(props.catalog, props.eventId, signal));
const history = useEventsRequest((signal) => props.client.getTrackingHistory(props.catalog, props.eventId, signal));
const save = useEventsRequest((signal, spec: TrackingSpec) =>
  props.client.putTracking(props.catalog, props.eventId, spec, signal),
);
const remove = useEventsRequest((signal, revision: number) =>
  props.client.deleteTracking(props.catalog, props.eventId, revision, signal),
);

const tracking = shallowRef<TrackingModel | null>(null);
const stage = shallowRef('');
const priority = shallowRef<TrackingPriority>('normal');
const notes = shallowRef('');
const fieldValues = shallowRef<Record<string, TrackingFieldValue>>({});
const savedNotice = shallowRef(false);
const confirmingRemove = shallowRef(false);

const stages = computed(() => workflow.value.value?.stages ?? []);
const definitions = computed(() =>
  trackingFieldDefinitions(workflow.value.value?.fields, tracking.value?.fields, props.locale),
);
const loading = computed(() => (workflow.pending.value || tracked.pending.value) && !workflow.value.value);
const loadFailure = computed(() => workflow.failure.value ?? tracked.failure.value);
const actionFailure = computed(() => save.failure.value ?? remove.failure.value);

function fill(current: TrackingModel | null, flow: WorkflowModel | null) {
  tracking.value = current;
  stage.value = current?.stage ?? flow?.defaultStage ?? '';
  priority.value = current?.priority ?? 'normal';
  notes.value = current?.notes ?? '';
  fieldValues.value = trackingFieldValues(
    trackingFieldDefinitions(flow?.fields, current?.fields, props.locale),
    current?.fields,
  );
}

async function load() {
  savedNotice.value = false;
  confirmingRemove.value = false;
  save.failure.value = null;
  remove.failure.value = null;
  const [flow, current] = await Promise.all([workflow.run(), tracked.run()]);
  const record = (current as TrackedEventModel | null)?.tracking ?? null;
  fill(record, flow);
  if (record) await history.run();
  else history.value.value = [];
}

function setField(key: string, value: TrackingFieldValue) {
  fieldValues.value = { ...fieldValues.value, [key]: value };
}

async function submit() {
  savedNotice.value = false;
  const current = tracking.value;
  const result = await save.run({
    stage: stage.value || null,
    priority: priority.value,
    notes: notes.value.trim(),
    assignee: current?.assignee ?? null,
    fields: trackingFieldsPayload(fieldValues.value, current?.fields),
    references: current?.references ?? [],
    expectedRevision: current?.revision ?? null,
  });
  if (!result) return;
  fill(result, workflow.value.value);
  savedNotice.value = true;
  emit('saved', result);
  await history.run();
}

async function stopTracking() {
  if (!tracking.value) return;
  if (!confirmingRemove.value) {
    confirmingRemove.value = true;
    return;
  }
  const result = await remove.run(tracking.value.revision);
  confirmingRemove.value = false;
  if (!result) return;
  fill(null, workflow.value.value);
  history.value.value = [];
  emit('removed');
}

const asPriority = (value: string) => value as TrackingPriority;
const inputClass = 'rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground';

onMounted(load);
watch([() => props.catalog, () => props.eventId, () => props.client], load);
</script>

<template>
  <section class="space-y-4 rounded-lg border border-border p-4" aria-labelledby="tracking-form-title">
    <h2 id="tracking-form-title" class="text-base font-semibold">{{ eventsText('tracking', locale) }}</h2>
    <div v-if="loading" class="space-y-2" aria-hidden="true">
      <SkeletonState v-for="index in 3" :key="index" class="h-9 w-full" />
    </div>
    <FailureNotice v-else-if="loadFailure" :failure="loadFailure" :locale="locale" retry @retry="load" />
    <form v-else class="grid gap-4" novalidate @submit.prevent="submit">
      <p v-if="!tracking" class="text-sm text-muted-foreground">{{ eventsText('notTracked', locale) }}</p>
      <fieldset class="grid gap-4 sm:grid-cols-2" :disabled="!canEdit || save.pending.value">
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium">{{ eventsText('stage', locale) }}</span>
          <select v-model="stage" name="stage" :class="inputClass">
            <option v-for="item in stages" :key="item.key" :value="item.key">
              {{ EventsLabels.stage(item.key, stages, locale) }}
            </option>
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium">{{ eventsText('priority', locale) }}</span>
          <select
            name="priority"
            :class="inputClass"
            :value="priority"
            @change="priority = asPriority(($event.target as HTMLSelectElement).value)"
          >
            <option v-for="item in TrackingPriorityValues" :key="item" :value="item">
              {{ EventsLabels.priority(item, locale) }}
            </option>
          </select>
        </label>
      </fieldset>
      <label class="flex flex-col gap-1 text-sm">
        <span class="font-medium">{{ eventsText('notes', locale) }}</span>
        <TextAreaInput
          v-model="notes"
          name="notes"
          :rows="4"
          :maxlength="EventsConstants.maxNotesLength"
          :is-disabled="!canEdit || save.pending.value"
        />
      </label>
      <fieldset v-if="definitions.length" class="grid gap-3" :disabled="!canEdit || save.pending.value">
        <legend class="mb-1 text-sm font-medium">{{ eventsText('fields', locale) }}</legend>
        <label
          v-for="definition in definitions"
          :key="definition.key"
          class="flex gap-1 text-sm"
          :class="definition.type === 'boolean' ? 'flex-row items-center gap-2' : 'flex-col'"
        >
          <span class="text-muted-foreground">{{ definition.label }}</span>
          <input
            v-if="definition.type === 'boolean'"
            type="checkbox"
            :name="`field-${definition.key}`"
            :checked="fieldValues[definition.key] === true"
            @change="setField(definition.key, ($event.target as HTMLInputElement).checked)"
          />
          <input
            v-else-if="definition.type === 'number'"
            type="number"
            :name="`field-${definition.key}`"
            :class="inputClass"
            :value="fieldValues[definition.key] ?? ''"
            @input="
              setField(
                definition.key,
                ($event.target as HTMLInputElement).value === ''
                  ? null
                  : Number(($event.target as HTMLInputElement).value),
              )
            "
          />
          <textarea
            v-else
            :name="`field-${definition.key}`"
            :class="inputClass"
            :rows="definition.multiline ? 2 : 1"
            :value="String(fieldValues[definition.key] ?? '')"
            @input="setField(definition.key, ($event.target as HTMLTextAreaElement).value)"
          ></textarea>
        </label>
      </fieldset>

      <FailureNotice v-if="actionFailure" :failure="actionFailure" :locale="locale" retry @retry="load" />
      <p v-if="savedNotice" class="text-sm text-success" role="status">{{ eventsText('saved', locale) }}</p>

      <div v-if="canEdit" class="flex flex-wrap gap-2">
        <Button type="submit" :is-loading="save.pending.value">{{ eventsText('saveTracking', locale) }}</Button>
        <Button
          v-if="tracking"
          type="button"
          variant="ghost"
          tone="danger"
          :is-loading="remove.pending.value"
          @click="stopTracking"
        >
          {{ confirmingRemove ? eventsText('confirmStopTracking', locale) : eventsText('stopTracking', locale) }}
        </Button>
      </div>

      <section v-if="history.value.value?.length" class="space-y-2" aria-labelledby="tracking-history-title">
        <h3 id="tracking-history-title" class="text-sm font-medium">{{ eventsText('history', locale) }}</h3>
        <ol class="space-y-1 text-sm text-muted-foreground">
          <li v-for="entry in history.value.value" :key="`${entry.atUtc}-${entry.toStage}`">
            <time :datetime="entry.atUtc">{{ formatTimestamp(entry.atUtc, locale) }}</time>
            ·
            {{ entry.fromStage ? `${EventsLabels.stage(entry.fromStage, stages, locale)} → ` : ''
            }}{{ EventsLabels.stage(entry.toStage, stages, locale) }}
          </li>
        </ol>
      </section>
    </form>
  </section>
</template>
