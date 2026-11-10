<script setup lang="ts">
import { computed } from 'vue';
import type { EventModel } from '../../contracts';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';

/** Shows who reviewed an event and why: a classifier's auto-acceptance, a person's review, or a held candidate's reason. */
defineOptions({ name: 'ReviewAttribution' });

const props = withDefaults(defineProps<{ event: EventModel; locale?: EventsLocale }>(), { locale: 'en' });

const line = computed(() => {
  const { reviewedBy, reviewReason, review } = props.event;
  const reason = reviewReason?.trim() ?? '';
  if (review === 'candidate') return reason ? eventsText('heldReason', props.locale, { reason }) : '';
  if (!reviewedBy) return '';
  const separator = reviewedBy.indexOf(':');
  const kind = separator < 0 ? '' : reviewedBy.slice(0, separator);
  const name = separator < 0 ? reviewedBy : reviewedBy.slice(separator + 1);
  const who =
    kind === 'classifier'
      ? eventsText('autoAcceptedBy', props.locale, { name })
      : eventsText('reviewedBy', props.locale, { name });
  return reason ? `${who} — ${reason}` : who;
});
</script>

<template>
  <p v-if="line" class="text-xs text-muted-foreground" data-test="review-attribution">{{ line }}</p>
</template>
