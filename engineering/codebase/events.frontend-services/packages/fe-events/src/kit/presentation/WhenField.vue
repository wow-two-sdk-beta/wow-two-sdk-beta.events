<script setup lang="ts">
import { computed } from 'vue';
import {
  DateCertaintyValues,
  DatePrecisionValues,
  type DateCertainty,
  type DatePrecision,
  type EventWhen,
} from '../../contracts';
import { EventsLabels } from '../text/EventsLabels';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';

/** Edits one honest date: how much is known, the value in that shape, and how sure it is. */
defineOptions({ name: 'WhenField' });

const props = withDefaults(
  defineProps<{
    modelValue: EventWhen;
    label: string;
    /** Prefixes the field names, so two dates in one form stay apart. */
    name: string;
    locale?: EventsLocale;
    disabled?: boolean;
  }>(),
  { locale: 'en', disabled: false },
);

const emit = defineEmits<{ 'update:modelValue': [value: EventWhen] }>();

const placeholders: Record<DatePrecision, string> = {
  unknown: '',
  year: '2026',
  month: '2026-03',
  day: '2026-03-14',
  minute: '2026-03-14T19:00',
};
const placeholder = computed(() => placeholders[props.modelValue.precision]);

function update(change: Partial<EventWhen>) {
  const next = { ...props.modelValue, ...change };
  emit('update:modelValue', next.precision === 'unknown' ? { ...next, value: null } : next);
}
const asPrecision = (value: string) => value as DatePrecision;
const asCertainty = (value: string) => value as DateCertainty;
</script>

<template>
  <fieldset class="grid gap-2 sm:grid-cols-3" :disabled="disabled">
    <legend class="mb-1 text-sm font-medium">{{ label }}</legend>
    <label class="flex flex-col gap-1 text-xs text-muted-foreground">
      {{ eventsText('precision', locale) }}
      <select
        :name="`${name}-precision`"
        class="rounded-md border border-border bg-background px-2 py-2 text-sm text-foreground"
        :value="modelValue.precision"
        @change="update({ precision: asPrecision(($event.target as HTMLSelectElement).value) })"
      >
        <option v-for="item in DatePrecisionValues" :key="item" :value="item">
          {{ EventsLabels.precision(item, locale) }}
        </option>
      </select>
    </label>
    <label class="flex flex-col gap-1 text-xs text-muted-foreground">
      {{ eventsText('date', locale) }}
      <input
        :name="`${name}-value`"
        class="rounded-md border border-border bg-background px-2 py-2 text-sm text-foreground"
        :value="modelValue.value ?? ''"
        :placeholder="placeholder"
        :disabled="disabled || modelValue.precision === 'unknown'"
        autocomplete="off"
        @input="update({ value: ($event.target as HTMLInputElement).value })"
      />
    </label>
    <label class="flex flex-col gap-1 text-xs text-muted-foreground">
      {{ eventsText('certainty', locale) }}
      <select
        :name="`${name}-certainty`"
        class="rounded-md border border-border bg-background px-2 py-2 text-sm text-foreground"
        :value="modelValue.certainty"
        @change="update({ certainty: asCertainty(($event.target as HTMLSelectElement).value) })"
      >
        <option v-for="item in DateCertaintyValues" :key="item" :value="item">
          {{ EventsLabels.certainty(item, locale) }}
        </option>
      </select>
    </label>
  </fieldset>
</template>
