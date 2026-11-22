<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { DateRangePicker } from '@wow-two-beta/fe/presentation/forms';
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
</script>

<template>
  <DateRangePicker
    v-if="custom"
    :placeholder="eventsText('customRange', locale)"
    data-test="custom-range"
    @update:model-value="(range) => emit('range', range)"
  />
  <label v-if="tracking" class="flex items-center max-sm:min-w-40 max-sm:grow">
    <span class="sr-only">{{ eventsText('sort', locale) }}</span>
    <select v-model="sort" name="sort" :class="feedSortClass">
      <option v-for="option in sortOptions" :key="option.value" :value="option.value">
        {{ eventsText(option.key, locale) }}
      </option>
    </select>
  </label>
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
