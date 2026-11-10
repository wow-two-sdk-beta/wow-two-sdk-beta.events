<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { shallowRef } from 'vue';
import type { CatalogKindModel, EventModel } from '../../contracts';
import type { EventsClient } from '../client/CreateEventsClient';
import { eventFactsDraft, validateEventFactsDraft, type EventFactsDraft } from '../facts/EventFactsDraft';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../text/EventsMessages';
import EventFactsFields from './EventFactsFields.vue';
import FailureNotice from './FailureNotice.vue';

/**
 * Lets a member propose an event: it lands as a candidate awaiting review (`POST …/candidates`) and is never published
 * by sending it.
 */
defineOptions({ name: 'EventProposalForm' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    locale?: EventsLocale;
    kinds?: readonly CatalogKindModel[];
  }>(),
  { locale: 'en', kinds: undefined },
);

const emit = defineEmits<{
  /** Fires with the candidate the service created. */
  proposed: [event: EventModel];
  cancel: [];
}>();

const draft = shallowRef<EventFactsDraft>(eventFactsDraft(undefined, props.locale));
const problems = shallowRef<readonly EventsMessageKey[]>([]);
const propose = useEventsRequest((signal, spec: Parameters<EventsClient['proposeEvent']>[1]) =>
  props.client.proposeEvent(props.catalog, spec, signal),
);

async function submit() {
  const checked = validateEventFactsDraft(draft.value);
  problems.value = checked.problems;
  if (!checked.facts) return;
  const event = await propose.run({ facts: checked.facts });
  if (event) {
    draft.value = eventFactsDraft(undefined, props.locale);
    emit('proposed', event);
  }
}
</script>

<template>
  <form class="grid gap-4" novalidate @submit.prevent="submit">
    <EventFactsFields v-model="draft" :locale="locale" :kinds="kinds" :disabled="propose.pending.value" />
    <ul v-if="problems.length" class="list-disc pl-5 text-sm text-destructive" role="alert">
      <li v-for="problem in problems" :key="problem">{{ eventsText(problem, locale) }}</li>
    </ul>
    <FailureNotice v-if="propose.failure.value" :failure="propose.failure.value" :locale="locale" />
    <div class="flex flex-wrap gap-2">
      <Button type="submit" :is-loading="propose.pending.value">{{ eventsText('sendForReview', locale) }}</Button>
      <Button type="button" variant="ghost" :is-disabled="propose.pending.value" @click="emit('cancel')">
        {{ eventsText('cancel', locale) }}
      </Button>
    </div>
  </form>
</template>
