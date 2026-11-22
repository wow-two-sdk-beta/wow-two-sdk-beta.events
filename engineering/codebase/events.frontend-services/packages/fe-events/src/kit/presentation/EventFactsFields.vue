<script setup lang="ts">
import {
  CountryPicker,
  SelectPicker,
  SelectPickerContent,
  SelectPickerItem,
  SelectPickerTrigger,
  SelectPickerValue,
  TextInput,
} from '@wow-two-beta/fe/presentation/forms';
import { computed } from 'vue';
import { EventStatusValues, type CatalogKindModel } from '../../contracts';
import type { EventFactsDraft } from '../facts/EventFactsDraft';
import { countryName } from '../facts/EventFactsText';
import { regionOfCountry, type EventRegion } from '../facts/EventRegions';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import { eventFieldClass, eventLabelClass } from './EventFormStyles';
import WhenField from './WhenField.vue';

/**
 * Edits an event's main facts in a draft: title, kind, status, dates, place (the country from the fe sdk's
 * `CountryPicker`, which stores its ISO alpha-2 code, with the countries of the consumer's `regions` listed first and
 * the scope the country derives shown under it), organizer and website — and, when asked, the application deadline
 * and where it was found.
 */
defineOptions({ name: 'EventFactsFields' });

const draft = defineModel<EventFactsDraft>({ required: true });

const props = withDefaults(
  defineProps<{
    locale?: EventsLocale;
    kinds?: readonly CatalogKindModel[];
    /** Whether the status is offered; a member's proposal leaves it at scheduled. */
    withStatus?: boolean;
    /** Whether the application deadline (a day) is offered. */
    withDeadline?: boolean;
    /** Whether "Where it was found" is offered: a link that stands in for the website when it has none. */
    withFoundAt?: boolean;
    /** The consumer's regions: their countries head the country picker and the form shows the scope the country derives. */
    regions?: readonly EventRegion[];
    disabled?: boolean;
  }>(),
  {
    locale: 'en',
    kinds: undefined,
    withStatus: false,
    withDeadline: false,
    withFoundAt: false,
    regions: undefined,
    disabled: false,
  },
);

const scope = computed(() => {
  if (!props.regions?.length) return null;
  const region = regionOfCountry(draft.value.country, props.regions);
  if (!region) return eventsText('scopeUntilCountry', props.locale);
  const label = pickText(region.label, props.locale) || region.key;
  return `${label}: ${countryName(draft.value.country.trim().toUpperCase(), props.locale) ?? draft.value.country}`;
});

/** The countries of the consumer's regions, uppercase and without repeats: the picker lists them first. */
const preferred = computed(() => [
  ...new Set((props.regions ?? []).flatMap((region) => region.countries ?? []).map((code) => code.toUpperCase())),
]);
const country = computed(() => draft.value.country.trim().toUpperCase() || null);

/** Replaces one field, so the parent's draft changes only through the model. */
function set<K extends keyof EventFactsDraft>(key: K, value: EventFactsDraft[K]) {
  draft.value = { ...draft.value, [key]: value };
}
</script>

<template>
  <div class="grid gap-4">
    <label class="flex flex-col gap-1 text-sm">
      <span :class="eventLabelClass">{{ eventsText('title', locale) }}</span>
      <TextInput
        :model-value="draft.title"
        name="title"
        :maxlength="200"
        autocomplete="off"
        :is-disabled="disabled"
        @update:model-value="set('title', $event)"
      />
    </label>
    <!-- Kind alone takes the full row, like the title; beside the status it shares the row. -->
    <div class="grid gap-4" :class="{ 'sm:grid-cols-2': withStatus }">
      <div class="flex flex-col gap-1 text-sm">
        <span :class="eventLabelClass">{{ eventsText('kind', locale) }}</span>
        <SelectPicker
          v-if="kinds?.length"
          :model-value="draft.kind"
          name="kind"
          :is-disabled="disabled"
          @update:model-value="set('kind', $event ?? '')"
        >
          <SelectPickerTrigger v-bind="{ 'aria-label': eventsText('kind', locale) }" data-test="facts-kind">
            <SelectPickerValue />
          </SelectPickerTrigger>
          <SelectPickerContent>
            <SelectPickerItem item-key="" label="—" />
            <SelectPickerItem
              v-for="item in kinds"
              :key="item.key"
              :item-key="item.key"
              :label="pickText(item.label, locale) || item.key"
            />
          </SelectPickerContent>
        </SelectPicker>
        <TextInput
          v-else
          :model-value="draft.kind"
          name="kind"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('kind', $event)"
        />
      </div>
      <div v-if="withStatus" class="flex flex-col gap-1 text-sm">
        <span :class="eventLabelClass">{{ eventsText('status', locale) }}</span>
        <SelectPicker
          :model-value="draft.status"
          name="status"
          :is-disabled="disabled"
          @update:model-value="(key) => key && set('status', key)"
        >
          <SelectPickerTrigger v-bind="{ 'aria-label': eventsText('status', locale) }" data-test="facts-status">
            <SelectPickerValue />
          </SelectPickerTrigger>
          <SelectPickerContent>
            <SelectPickerItem
              v-for="item in EventStatusValues"
              :key="item"
              :item-key="item"
              :label="EventsLabels.status(item, locale)"
            />
          </SelectPickerContent>
        </SelectPicker>
      </div>
    </div>
    <WhenField
      :model-value="draft.starts"
      name="starts"
      :label="eventsText('starts', locale)"
      :locale="locale"
      :disabled="disabled"
      @update:model-value="set('starts', $event)"
    />
    <WhenField
      :model-value="draft.ends"
      name="ends"
      :label="eventsText('ends', locale)"
      :locale="locale"
      :disabled="disabled"
      @update:model-value="set('ends', $event)"
    />
    <div class="grid gap-4 sm:grid-cols-3">
      <label class="flex flex-col gap-1 text-sm">
        <span :class="eventLabelClass">{{ eventsText('venue', locale) }}</span>
        <TextInput
          :model-value="draft.venue"
          name="venue"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('venue', $event)"
        />
      </label>
      <label class="flex flex-col gap-1 text-sm">
        <span :class="eventLabelClass">{{ eventsText('city', locale) }}</span>
        <TextInput
          :model-value="draft.city"
          name="city"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('city', $event)"
        />
      </label>
      <div class="flex flex-col gap-1 text-sm">
        <span :class="eventLabelClass">{{ eventsText('country', locale) }}</span>
        <CountryPicker
          :model-value="country"
          name="country"
          :locale="locale"
          :preferred="preferred.length ? preferred : undefined"
          :placeholder="eventsText('selectCountry', locale)"
          :search-placeholder="eventsText('searchCountries', locale)"
          :no-results-label="eventsText('noCountries', locale)"
          v-bind="{ 'aria-label': eventsText('country', locale) }"
          :is-disabled="disabled"
          is-clearable
          @update:model-value="set('country', $event ?? '')"
        />
        <span v-if="scope" class="text-xs text-muted-foreground" data-test="facts-scope">{{ scope }}</span>
      </div>
    </div>
    <label v-if="withDeadline" class="flex flex-col gap-1 text-sm sm:max-w-xs">
      <span :class="eventLabelClass">{{ eventsText('applicationDeadline', locale) }}</span>
      <input
        name="applicationDeadline"
        type="date"
        :class="eventFieldClass"
        :value="draft.applicationDeadline"
        :disabled="disabled"
        @input="set('applicationDeadline', ($event.target as HTMLInputElement).value)"
      />
    </label>
    <div class="grid gap-4 sm:grid-cols-2">
      <label class="flex flex-col gap-1 text-sm">
        <span :class="eventLabelClass">{{ eventsText('organizer', locale) }}</span>
        <TextInput
          :model-value="draft.organizer"
          name="organizer"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('organizer', $event)"
        />
      </label>
      <label class="flex flex-col gap-1 text-sm">
        <span :class="eventLabelClass">{{ eventsText('website', locale) }}</span>
        <input
          name="website"
          type="url"
          autocomplete="off"
          :class="eventFieldClass"
          :value="draft.website"
          :disabled="disabled"
          @input="set('website', ($event.target as HTMLInputElement).value)"
        />
      </label>
    </div>
    <label v-if="withFoundAt" class="flex flex-col gap-1 text-sm">
      <span :class="eventLabelClass">{{ eventsText('foundAt', locale) }}</span>
      <input
        name="foundAt"
        type="url"
        autocomplete="off"
        :class="eventFieldClass"
        :value="draft.foundAt"
        :disabled="disabled"
        @input="set('foundAt', ($event.target as HTMLInputElement).value)"
      />
    </label>
  </div>
</template>
