<script setup lang="ts">
import { EventMediaCard, toEventsLocale } from '@wow-two-beta/fe-events/kit';
import { computed, onMounted, ref } from 'vue';
import expoPoster from './posters/poster-expo.svg?no-inline';
import runwayPoster from './posters/poster-runway.svg?no-inline';
import showcasePoster from './posters/poster-showcase.svg?no-inline';
import {
  cardSamples,
  sampleDefaultStage,
  sampleKinds,
  sampleQuickStages,
  sampleStages,
  sampleToday,
} from './CardSamples';

/**
 * Renders sample event cards in the feed's grid at four viewport widths — 1440, 1024, 768 and 390 — to check the
 * columns and each card state by eye. A development page only: `cards.html`, served by the dev server and left out of
 * the build. The posters are imported as files (`?no-inline`) and resolve against the page's address, since the card
 * shows only http(s) images. `?locale=ru` or `?locale=uz` writes the cards in that language (English by default), to
 * check the date tile's month names at every width.
 */
defineOptions({ name: 'CardGallery' });

/** The page's side gutters (1.5rem each) the content width gives up; the console's rail is not drawn here. */
const Gutter = 48;
const viewports = [1440, 1024, 768, 390];
const locale = toEventsLocale(new URLSearchParams(window.location.search).get('locale'));
const absolute = (url: string) => new URL(url, window.location.href).href;
const samples = computed(() =>
  cardSamples({ runway: absolute(runwayPoster), expo: absolute(expoPoster), showcase: absolute(showcasePoster) }),
);
const grids = ref<HTMLElement[]>([]);
const columns = ref<number[]>([]);
const last = ref('');

/** Names a frame by its viewport and, once measured, how many columns the grid made. */
function heading(viewport: number, index: number): string {
  const count = columns.value[index];
  return count ? `${viewport} px viewport · ${count} ${count === 1 ? 'column' : 'columns'}` : `${viewport} px viewport`;
}

onMounted(() => {
  columns.value = grids.value.map(
    (grid) => getComputedStyle(grid).gridTemplateColumns.split(' ').filter(Boolean).length,
  );
});
</script>

<template>
  <main class="mx-auto max-w-[1500px] space-y-10 p-6">
    <header class="space-y-1">
      <h1 class="text-lg font-semibold">Event cards</h1>
      <p class="text-sm text-muted-foreground" role="status" data-test="gallery-last">
        {{ last || 'Open a card or pick a stage to see its event here.' }}
      </p>
    </header>
    <section v-for="(viewport, index) in viewports" :key="viewport" class="space-y-3" data-test="gallery-frame">
      <h2 class="text-sm font-semibold text-muted-foreground">{{ heading(viewport, index) }}</h2>
      <div class="max-w-full overflow-x-auto" :style="{ width: `${viewport - Gutter}px` }">
        <ul
          :ref="
            (element) => {
              if (element) grids[index] = element as HTMLElement;
            }
          "
          class="grid grid-cols-[repeat(auto-fill,minmax(min(100%,17rem),1fr))] gap-3"
          :aria-label="`Sample cards at ${viewport} px`"
        >
          <li v-for="sample in samples" :key="sample.event.id" class="min-w-0" :title="sample.label">
            <EventMediaCard
              :event="sample.event"
              :locale="locale"
              :today="sampleToday"
              :kinds="sampleKinds"
              :href="`#${sample.event.id}`"
              :tracking="sample.tracking"
              :stages="sampleStages"
              :default-stage="sampleDefaultStage"
              :quick-stages="sampleQuickStages"
              can-track
              @open="(id) => (last = `open ${id}`)"
              @track="(id, stage) => (last = `track ${id} → ${stage}`)"
            />
          </li>
        </ul>
      </div>
    </section>
  </main>
</template>
