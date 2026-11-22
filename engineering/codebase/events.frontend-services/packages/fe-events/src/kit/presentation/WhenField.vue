<script setup lang="ts">
import {
  SelectPicker,
  SelectPickerContent,
  SelectPickerItem,
  SelectPickerTrigger,
  SelectPickerValue,
} from '@wow-two-beta/fe/presentation/forms';
import { computed } from 'vue';
import { DateCertaintyValues, DatePrecisionValues, type DatePrecision, type EventWhen } from '../../contracts';
import { EventsLabels } from '../text/EventsLabels';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import { eventFieldClass, eventLabelClass } from './EventFormStyles';

/**
 * Edits one honest date: how much is known, the value in that shape, and how sure it is. The date box always carries a
 * hint: the shape the precision asks for, or — while the precision is "not announced" and the box is off — what to do.
 */
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

const shapes: Record<Exclude<DatePrecision, 'unknown'>, string> = {
  year: '2026',
  month: '2026-03',
  day: '2026-03-14',
  minute: '2026-03-14T19:00',
};
const placeholder = computed(() => {
  const precision = props.modelValue.precision;
  return precision === 'unknown' ? eventsText('dateHintUnknown', props.locale) : shapes[precision];
});

function update(change: Partial<EventWhen>) {
  const next = { ...props.modelValue, ...change };
  emit('update:modelValue', next.precision === 'unknown' ? { ...next, value: null } : next);
}
</script>

<template>
  <fieldset class="grid gap-2 sm:grid-cols-3" :disabled="disabled">
    <legend class="mb-1" :class="eventLabelClass">{{ label }}</legend>
    <div class="flex flex-col gap-1">
      <span :class="eventLabelClass">{{ eventsText('precision', locale) }}</span>
      <SelectPicker
        :model-value="modelValue.precision"
        :name="`${name}-precision`"
        :is-disabled="disabled"
        @update:model-value="(key) => key && update({ precision: key })"
      >
        <SelectPickerTrigger
          v-bind="{ 'aria-label': eventsText('precision', locale) }"
          :data-test="`${name}-precision`"
        >
          <SelectPickerValue />
        </SelectPickerTrigger>
        <SelectPickerContent>
          <SelectPickerItem
            v-for="item in DatePrecisionValues"
            :key="item"
            :item-key="item"
            :label="EventsLabels.precision(item, locale)"
          />
        </SelectPickerContent>
      </SelectPicker>
    </div>
    <label class="flex flex-col gap-1">
      <span :class="eventLabelClass">{{ eventsText('date', locale) }}</span>
      <input
        :name="`${name}-value`"
        :class="eventFieldClass"
        :value="modelValue.value ?? ''"
        :placeholder="placeholder"
        :disabled="disabled || modelValue.precision === 'unknown'"
        autocomplete="off"
        @input="update({ value: ($event.target as HTMLInputElement).value })"
      />
    </label>
    <div class="flex flex-col gap-1">
      <span :class="eventLabelClass">{{ eventsText('certainty', locale) }}</span>
      <SelectPicker
        :model-value="modelValue.certainty"
        :name="`${name}-certainty`"
        :is-disabled="disabled"
        @update:model-value="(key) => key && update({ certainty: key })"
      >
        <SelectPickerTrigger
          v-bind="{ 'aria-label': eventsText('certainty', locale) }"
          :data-test="`${name}-certainty`"
        >
          <SelectPickerValue />
        </SelectPickerTrigger>
        <SelectPickerContent>
          <SelectPickerItem
            v-for="item in DateCertaintyValues"
            :key="item"
            :item-key="item"
            :label="EventsLabels.certainty(item, locale)"
          />
        </SelectPickerContent>
      </SelectPicker>
    </div>
  </fieldset>
</template>
