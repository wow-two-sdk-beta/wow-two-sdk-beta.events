<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { computed } from 'vue';
import type { EventsFailure } from '../client/EventsFailure';
import { eventsFailureMessage, type EventsFailureContext } from '../text/EventsLabels';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';

/** Shows a failed Events call in words, with a retry when the screen offers one. */
defineOptions({ name: 'FailureNotice' });

const props = withDefaults(
  defineProps<{
    failure: EventsFailure;
    locale?: EventsLocale;
    /** What the screen needs, so a 403 names the missing permission. */
    context?: EventsFailureContext;
    /** Whether to offer a retry button. */
    retry?: boolean;
  }>(),
  { locale: 'en', context: 'read', retry: false },
);

const emit = defineEmits<{ retry: [] }>();

const message = computed(() => eventsFailureMessage(props.failure, props.locale, props.context));
</script>

<template>
  <div
    v-if="message"
    class="flex flex-wrap items-center gap-3 rounded-md border border-destructive/40 bg-destructive-soft px-3 py-2 text-sm text-destructive-soft-foreground"
    role="alert"
    :data-reason="failure.reason"
  >
    <span class="min-w-0 flex-1">{{ message }}</span>
    <Button v-if="retry" size="sm" variant="ghost" @click="emit('retry')">{{ eventsText('reload', locale) }}</Button>
  </div>
</template>
