<script setup lang="ts">
import { TextInput } from '@wow-two-beta/fe/presentation/forms';
import { computed } from 'vue';
import { EventStatusValues, type CatalogKindModel, type EventStatus } from '../../contracts';
import type { EventFactsDraft } from '../facts/EventFactsDraft';
import { countryName } from '../facts/EventFactsText';
import { regionOfCountry, type EventRegion } from '../facts/EventRegions';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import WhenField from './WhenField.vue';

/**
 * Edits an event's main facts in a draft: title, kind, status, dates, place (with the scope its country derives, when
 * regions are given), organizer and website — and, when asked, the application deadline and where it was found.
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
    /** The consumer's regions: the form shows the scope the country derives. */
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

const inputClass = 'rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground';
const asStatus = (value: string) => value as EventStatus;

/** Replaces one field, so the parent's draft changes only through the model. */
function set<K extends keyof EventFactsDraft>(key: K, value: EventFactsDraft[K]) {
  draft.value = { ...draft.value, [key]: value };
}
</script>

<template>
  <div class="grid gap-4">
    <label class="flex flex-col gap-1 text-sm">
      <span class="font-medium">{{ eventsText('title', locale) }}</span>
      <TextInput
        :model-value="draft.title"
        name="title"
        :maxlength="200"
        autocomplete="off"
        :is-disabled="disabled"
        @update:model-value="set('title', $event)"
      />
    </label>
    <div class="grid gap-4 sm:grid-cols-2">
      <label class="flex flex-col gap-1 text-sm">
        <span class="font-medium">{{ eventsText('kind', locale) }}</span>
        <select
          v-if="kinds?.length"
          :value="draft.kind"
          name="kind"
          :class="inputClass"
          :disabled="disabled"
          @change="set('kind', ($event.target as HTMLSelectElement).value)"
        >
          <option value="">—</option>
          <option v-for="item in kinds" :key="item.key" :value="item.key">
            {{ pickText(item.label, locale) || item.key }}
          </option>
        </select>
        <TextInput
          v-else
          :model-value="draft.kind"
          name="kind"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('kind', $event)"
        />
      </label>
      <label v-if="withStatus" class="flex flex-col gap-1 text-sm">
        <span class="font-medium">{{ eventsText('status', locale) }}</span>
        <select
          name="status"
          :class="inputClass"
          :value="draft.status"
          :disabled="disabled"
          @change="set('status', asStatus(($event.target as HTMLSelectElement).value))"
        >
          <option v-for="item in EventStatusValues" :key="item" :value="item">
            {{ EventsLabels.status(item, locale) }}
          </option>
        </select>
      </label>
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
        <span class="font-medium">{{ eventsText('venue', locale) }}</span>
        <TextInput
          :model-value="draft.venue"
          name="venue"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('venue', $event)"
        />
      </label>
      <label class="flex flex-col gap-1 text-sm">
        <span class="font-medium">{{ eventsText('city', locale) }}</span>
        <TextInput
          :model-value="draft.city"
          name="city"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('city', $event)"
        />
      </label>
      <label class="flex flex-col gap-1 text-sm">
        <span class="font-medium">{{ eventsText('country', locale) }}</span>
        <TextInput
          :model-value="draft.country"
          name="country"
          :maxlength="2"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('country', $event)"
        />
        <span v-if="scope" class="text-xs text-muted-foreground" data-test="facts-scope">{{ scope }}</span>
      </label>
    </div>
    <label v-if="withDeadline" class="flex flex-col gap-1 text-sm sm:max-w-xs">
      <span class="font-medium">{{ eventsText('applicationDeadline', locale) }}</span>
      <input
        name="applicationDeadline"
        type="date"
        :class="inputClass"
        :value="draft.applicationDeadline"
        :disabled="disabled"
        @input="set('applicationDeadline', ($event.target as HTMLInputElement).value)"
      />
    </label>
    <div class="grid gap-4 sm:grid-cols-2">
      <label class="flex flex-col gap-1 text-sm">
        <span class="font-medium">{{ eventsText('organizer', locale) }}</span>
        <TextInput
          :model-value="draft.organizer"
          name="organizer"
          autocomplete="off"
          :is-disabled="disabled"
          @update:model-value="set('organizer', $event)"
        />
      </label>
      <label class="flex flex-col gap-1 text-sm">
        <span class="font-medium">{{ eventsText('website', locale) }}</span>
        <input
          name="website"
          type="url"
          autocomplete="off"
          :class="inputClass"
          :value="draft.website"
          :disabled="disabled"
          @input="set('website', ($event.target as HTMLInputElement).value)"
        />
      </label>
    </div>
    <label v-if="withFoundAt" class="flex flex-col gap-1 text-sm">
      <span class="font-medium">{{ eventsText('foundAt', locale) }}</span>
      <input
        name="foundAt"
        type="url"
        autocomplete="off"
        :class="inputClass"
        :value="draft.foundAt"
        :disabled="disabled"
        @input="set('foundAt', ($event.target as HTMLInputElement).value)"
      />
    </label>
  </div>
</template>
