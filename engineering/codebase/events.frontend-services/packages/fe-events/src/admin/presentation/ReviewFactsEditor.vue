<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { shallowRef } from 'vue';
import type { CatalogKindModel, EventFacts } from '../../contracts';
import { eventFactsDraft, validateEventFactsDraft, type EventFactsDraft } from '../../kit/facts/EventFactsDraft';
import EventFactsFields from '../../kit/presentation/EventFactsFields.vue';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../../kit/text/EventsMessages';

/** Edits the facts a reviewer accepts or applies; facts it does not show pass through unchanged. */
defineOptions({ name: 'ReviewFactsEditor' });

const props = withDefaults(
  defineProps<{
    /** The facts the edit starts from: the candidate's, or the pending change's proposal. */
    facts: EventFacts;
    submitLabel: string;
    locale?: EventsLocale;
    kinds?: readonly CatalogKindModel[];
    pending?: boolean;
  }>(),
  { locale: 'en', kinds: undefined, pending: false },
);

const emit = defineEmits<{ submit: [facts: EventFacts]; cancel: [] }>();

const draft = shallowRef<EventFactsDraft>(eventFactsDraft(props.facts, props.locale));
const problems = shallowRef<readonly EventsMessageKey[]>([]);

function submit() {
  const checked = validateEventFactsDraft(draft.value, props.facts);
  problems.value = checked.problems;
  if (checked.facts) emit('submit', checked.facts);
}
</script>

<template>
  <form class="grid gap-4 rounded-md border border-border p-3" novalidate @submit.prevent="submit">
    <EventFactsFields v-model="draft" :locale="locale" :kinds="kinds" with-status :disabled="pending" />
    <ul v-if="problems.length" class="list-disc pl-5 text-sm text-destructive" role="alert">
      <li v-for="problem in problems" :key="problem">{{ eventsText(problem, locale) }}</li>
    </ul>
    <div class="flex flex-wrap gap-2">
      <Button type="submit" size="sm" :is-loading="pending">{{ submitLabel }}</Button>
      <Button type="button" size="sm" variant="ghost" :is-disabled="pending" @click="emit('cancel')">
        {{ eventsText('cancel', locale) }}
      </Button>
    </div>
  </form>
</template>
