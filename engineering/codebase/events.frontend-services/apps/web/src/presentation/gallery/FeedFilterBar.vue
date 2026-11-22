<script setup lang="ts">
import {
  eventsFilterLabels,
  type EventsChoiceFacet,
  type EventsFacet,
  type EventsFilters,
} from '@wow-two-beta/fe-events/kit';
import { computed } from 'vue';

/**
 * A small filter bar for the development feed gallery, built from the kit's headless `useEventsFilters` state alone: the
 * search box, the match pill, the inline facets as chips and the rest behind a "Filters" disclosure. The fe sdk ships no
 * facet bar and the kit none either, so a product writes its own; this one only lets the gallery drive `EventsFeed`.
 * A development helper: it is not part of the console and is left out of the build with the gallery.
 */
defineOptions({ name: 'FeedFilterBar' });

const props = defineProps<{ filters: EventsFilters }>();
// The state's refs are the page's to write; the bar holds them, so it mutates no prop.
const { search, selection, filtered, facetDefinitions, matchCount, totalCount } = props.filters;

const labels = eventsFilterLabels('en');
const chipClass = 'min-h-8 rounded-full border px-3 py-1 text-sm';
const pressed = 'border-brand bg-brand text-brand-foreground';
const idle = 'border-border bg-background';

const inline = computed(() => facetDefinitions.value.filter((facet) => facet.placement === 'inline'));
const behind = computed(() => facetDefinitions.value.filter((facet) => facet.placement === 'popover'));
const match = computed(() => {
  const matched = matchCount.value;
  if (matched === undefined) return '';
  const total = totalCount.value ?? matched;
  return labels.matchLabel.replace('{matched}', String(matched)).replace('{total}', String(total));
});

/** The values the selection holds for a choice facet, whether it is one string or a list. */
function picked(key: string): readonly string[] {
  const value = selection.value[key];
  return typeof value === 'string' ? [value] : Array.isArray(value) ? (value as readonly string[]) : [];
}

function write(key: string, value: string | readonly string[] | true | undefined) {
  const next = { ...selection.value };
  if (value === undefined) delete next[key];
  else next[key] = value;
  selection.value = next;
}

function choose(facet: EventsChoiceFacet, value: string) {
  const has = picked(facet.key).includes(value);
  if (facet.kind === 'single') return write(facet.key, has ? undefined : value);
  const list = has ? picked(facet.key).filter((item) => item !== value) : [...picked(facet.key), value];
  write(facet.key, list.length ? list : undefined);
}

const isOn = (facet: EventsFacet, value?: string) =>
  facet.kind === 'toggle' ? selection.value[facet.key] === true : picked(facet.key).includes(value ?? '');

function clearAll() {
  selection.value = {};
  search.value = '';
}
</script>

<template>
  <div class="space-y-3" data-test="feed-bar">
    <div class="flex flex-wrap items-center gap-2">
      <input
        type="search"
        class="min-h-10 min-w-48 grow rounded-full border border-border bg-background px-4 text-sm"
        :aria-label="labels.searchLabel"
        :placeholder="labels.searchPlaceholder"
        :value="search"
        @input="search = ($event.target as HTMLInputElement).value"
      />
      <span v-if="match" class="text-sm text-muted-foreground" data-test="feed-match">{{ match }}</span>
      <button
        v-if="filtered"
        type="button"
        class="min-h-10 rounded-full border border-border px-3 text-sm"
        data-test="feed-clear"
        @click="clearAll"
      >
        {{ labels.clearAllLabel }}
      </button>
    </div>

    <div v-for="facet in inline" :key="facet.key" class="flex flex-wrap items-center gap-2" :data-facet="facet.key">
      <span class="text-sm text-muted-foreground">{{ facet.label }}</span>
      <template v-if="facet.kind !== 'toggle'">
        <button
          v-for="option in facet.options"
          :key="option.value"
          type="button"
          :class="[chipClass, isOn(facet, option.value) ? pressed : idle]"
          :aria-pressed="isOn(facet, option.value)"
          @click="choose(facet, option.value)"
        >
          {{ option.label }}<template v-if="option.count !== undefined"> {{ option.count }}</template>
        </button>
      </template>
    </div>

    <details class="rounded-lg border border-border p-3" data-test="feed-filters">
      <summary class="cursor-pointer text-sm font-medium">{{ labels.filtersLabel }}</summary>
      <div class="mt-3 space-y-3">
        <div v-for="facet in behind" :key="facet.key" class="flex flex-wrap items-center gap-2" :data-facet="facet.key">
          <template v-if="facet.kind === 'toggle'">
            <button
              type="button"
              :class="[chipClass, isOn(facet) ? pressed : idle]"
              :aria-pressed="isOn(facet)"
              @click="write(facet.key, isOn(facet) ? undefined : true)"
            >
              {{ facet.label }}<template v-if="facet.count !== undefined"> {{ facet.count }}</template>
            </button>
          </template>
          <template v-else>
            <span class="text-sm text-muted-foreground">{{ facet.label }}</span>
            <button
              v-for="option in facet.options"
              :key="option.value"
              type="button"
              :class="[chipClass, isOn(facet, option.value) ? pressed : idle]"
              :aria-pressed="isOn(facet, option.value)"
              @click="choose(facet, option.value)"
            >
              {{ option.label }}<template v-if="option.count !== undefined"> {{ option.count }}</template>
            </button>
          </template>
        </div>
      </div>
    </details>
  </div>
</template>
