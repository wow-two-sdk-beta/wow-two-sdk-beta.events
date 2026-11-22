<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import {
  DateRangePicker,
  SelectPicker,
  SelectPickerContent,
  SelectPickerItem,
  SelectPickerTrigger,
  SelectPickerValue,
} from '@wow-two-beta/fe/presentation/forms';
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger, MenuItem } from '@wow-two-beta/fe/presentation/nav';
import type { TrackingSort } from '../../contracts';
import type { EventsLocale } from '../text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../text/EventsMessages';
import EventGlyph from './EventGlyph.vue';
import { feedSortClass } from './EventsFeedStyles';

/**
 * The feed's own controls: the custom range picker (the `custom` preset only), the sort (tracking only), the proposal
 * button and the overflow menu with the calendar download. `EventsFeed` renders them in its `events-actions` row, beside
 * the page's own filter bar.
 */
defineOptions({ name: 'EventsFeedActions' });

defineProps<{
  locale: EventsLocale;
  /** Whether the date preset is `custom`, which asks for a range. */
  custom: boolean;
  /** Whether the feed lists the organization's tracking, which can be sorted. */
  tracking: boolean;
  /** Whether the person may propose an event, and no proposal form is open. */
  proposable: boolean;
  /** Whether the calendar download is running. */
  calendarPending: boolean;
}>();

const sort = defineModel<TrackingSort>('sort', { required: true });

const emit = defineEmits<{
  range: [range: { start: { toString(): string }; end: { toString(): string } } | null];
  propose: [];
  download: [];
}>();

const sortOptions: readonly { value: TrackingSort; key: EventsMessageKey }[] = [
  { value: 'starts', key: 'sortStarts' },
  { value: 'added', key: 'sortAdded' },
  { value: 'deadline', key: 'sortDeadline' },
];

/** Takes the picked sort; the picker offers no clear, so a null key is no pick. */
function pickSort(key: TrackingSort | null) {
  if (key) sort.value = key;
}
</script>

<template>
  <DateRangePicker
    v-if="custom"
    :placeholder="eventsText('customRange', locale)"
    data-test="custom-range"
    @update:model-value="(range) => emit('range', range)"
  />
  <div v-if="tracking" class="max-sm:min-w-40 max-sm:grow sm:w-52">
    <SelectPicker :model-value="sort" name="sort" @update:model-value="pickSort">
      <SelectPickerTrigger
        :class="feedSortClass"
        v-bind="{ 'aria-label': eventsText('sort', locale) }"
        data-test="feed-sort"
      >
        <SelectPickerValue />
      </SelectPickerTrigger>
      <SelectPickerContent>
        <SelectPickerItem
          v-for="option in sortOptions"
          :key="option.value"
          :item-key="option.value"
          :label="eventsText(option.key, locale)"
        />
      </SelectPickerContent>
    </SelectPicker>
  </div>
  <Button v-if="proposable" size="md" variant="outline" class="max-sm:grow" @click="emit('propose')">
    {{ eventsText('proposeEvent', locale) }}
  </Button>
  <DropdownMenu placement="bottom-end">
    <DropdownMenuTrigger as-child>
      <Button
        variant="ghost"
        size="md"
        shape="circle"
        :aria-label="eventsText('moreActions', locale)"
        :is-disabled="calendarPending"
        data-test="feed-overflow"
      >
        <EventGlyph name="more" :size="18" />
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent v-bind="{ 'aria-label': eventsText('moreActions', locale) }">
      <MenuItem class="max-sm:min-h-10" data-test="download-calendar" @select="emit('download')">
        {{ eventsText('downloadCalendar', locale) }}
      </MenuItem>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
