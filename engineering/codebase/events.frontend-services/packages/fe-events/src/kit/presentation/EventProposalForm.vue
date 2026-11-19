<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { shallowRef } from 'vue';
import type { CatalogKindModel, EventModel, JsonValue, TrackingModel, TrackingSpec } from '../../contracts';
import type { EventsClient } from '../client/CreateEventsClient';
import { eventFactsDraft, validateEventFactsDraft, type EventFactsDraft } from '../facts/EventFactsDraft';
import type { EventRegion } from '../facts/EventRegions';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../text/EventsMessages';
import EventFactsFields from './EventFactsFields.vue';
import FailureNotice from './FailureNotice.vue';

/**
 * Lets a member propose an event (`POST …/candidates`): in a private catalog, whose only readers are the member's own
 * organization, it publishes at once; in a shared or public one it lands as a candidate awaiting review. With
 * `initialTracking`, the organization's tracking of the new event is written right after (Ocharo: shortlisted, with its
 * cost and participation).
 */
defineOptions({ name: 'EventProposalForm' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    locale?: EventsLocale;
    kinds?: readonly CatalogKindModel[];
    /** The tracking written after the event is created; none by default. */
    initialTracking?: TrackingSpec;
    /** The tracking field "Where it was found" is also stored in (Ocharo: `sourceUrl`), with `initialTracking`. */
    sourceField?: string;
    /** Whether the application deadline is offered. */
    withDeadline?: boolean;
    /** Whether "Where it was found" is offered. */
    withFoundAt?: boolean;
    /** The consumer's regions: the form shows the scope the country derives. */
    regions?: readonly EventRegion[];
    /** Whether the catalog is private, so the event publishes at once: the button reads "Add event". */
    privateCatalog?: boolean;
  }>(),
  {
    locale: 'en',
    kinds: undefined,
    initialTracking: undefined,
    sourceField: undefined,
    withDeadline: false,
    withFoundAt: false,
    regions: undefined,
    privateCatalog: false,
  },
);

const emit = defineEmits<{
  /**
   * Fires with the event the service created — published, or a candidate awaiting review — and the tracking written
   * with it: null when none was asked for or its write failed.
   */
  proposed: [event: EventModel, tracking: TrackingModel | null];
  cancel: [];
}>();

const draft = shallowRef<EventFactsDraft>(eventFactsDraft(undefined, props.locale));
const problems = shallowRef<readonly EventsMessageKey[]>([]);
const propose = useEventsRequest((signal, spec: Parameters<EventsClient['proposeEvent']>[1]) =>
  props.client.proposeEvent(props.catalog, spec, signal),
);
const track = useEventsRequest((signal, eventId: string, spec: TrackingSpec) =>
  props.client.putTracking(props.catalog, eventId, spec, signal),
);

function trackingSpec(foundAt: string): TrackingSpec | null {
  if (!props.initialTracking) return null;
  const base = props.initialTracking.fields;
  const fields: JsonValue | undefined =
    props.sourceField && foundAt
      ? { ...(base && typeof base === 'object' && !Array.isArray(base) ? base : {}), [props.sourceField]: foundAt }
      : base;
  return { ...props.initialTracking, ...(fields !== undefined ? { fields } : {}), expectedRevision: null };
}

async function submit() {
  const checked = validateEventFactsDraft(draft.value);
  problems.value = checked.problems;
  if (!checked.facts) return;
  const foundAt = draft.value.foundAt.trim();
  const event = await propose.run({ facts: checked.facts });
  if (!event) return;
  const spec = trackingSpec(foundAt);
  const tracking = spec ? await track.run(event.id, spec) : null;
  draft.value = eventFactsDraft(undefined, props.locale);
  emit('proposed', event, tracking);
}
</script>

<template>
  <form class="grid gap-4" novalidate @submit.prevent="submit">
    <EventFactsFields
      v-model="draft"
      :locale="locale"
      :kinds="kinds"
      :regions="regions"
      :with-deadline="withDeadline"
      :with-found-at="withFoundAt"
      :disabled="propose.pending.value || track.pending.value"
    />
    <ul v-if="problems.length" class="list-disc pl-5 text-sm text-destructive" role="alert">
      <li v-for="problem in problems" :key="problem">{{ eventsText(problem, locale) }}</li>
    </ul>
    <FailureNotice v-if="propose.failure.value" :failure="propose.failure.value" :locale="locale" />
    <div class="flex flex-wrap gap-2">
      <Button type="submit" :is-loading="propose.pending.value || track.pending.value">
        {{ eventsText(privateCatalog ? 'addEvent' : 'sendForReview', locale) }}
      </Button>
      <Button type="button" variant="ghost" :is-disabled="propose.pending.value" @click="emit('cancel')">
        {{ eventsText('cancel', locale) }}
      </Button>
    </div>
  </form>
</template>
