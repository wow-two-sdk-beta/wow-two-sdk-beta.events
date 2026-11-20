<script setup lang="ts">
import { EventsFeed, eventTitle, formatEventWhen } from '@wow-two-beta/fe-events/kit';
import { ref } from 'vue';
import expoPoster from './posters/poster-expo.svg?no-inline';
import runwayPoster from './posters/poster-runway.svg?no-inline';
import showcasePoster from './posters/poster-showcase.svg?no-inline';
import { sampleKinds, sampleQuickStages, sampleToday } from './CardSamples';
import { createFeedFake, feedRegions, feedWorkflow } from './FeedFake';

/**
 * Renders `EventsFeed` in tracking mode over an in-memory service (`FeedFake`: 40 events, pages of 12, facets that
 * follow the query), with Ocharo's regions, kinds, workflow, hidden stages and quick stages and one consumer attention
 * pill, to check the facet bar, the scroll and the cards by eye at any width. A development page only: `feed.html`,
 * served by the dev server and left out of the build.
 */
defineOptions({ name: 'FeedGallery' });

const props = withDefaults(defineProps<{ latencyMs?: number }>(), { latencyMs: 120 });

const absolute = (url: string) => new URL(url, window.location.href).href;
const fake = createFeedFake({
  posters: [absolute(runwayPoster), absolute(expoPoster), absolute(showcasePoster)],
  latencyMs: props.latencyMs,
});
const pastEvents = fake.events.filter((event) => (event.facts.starts?.value ?? '9') < sampleToday);
const attention = [{ key: 'past', label: `${pastEvents.length} past to close`, count: pastEvents.length }];
const last = ref('');
</script>

<template>
  <main class="mx-auto max-w-[1500px] space-y-4 p-6">
    <header class="space-y-1">
      <h1 class="text-lg font-semibold">Events feed</h1>
      <p class="text-sm text-muted-foreground" role="status" data-test="feed-last">
        {{ last || 'Open a card, pick a stage, or add an event to see it here.' }}
      </p>
    </header>
    <EventsFeed
      :client="fake.client"
      catalog="ocharo"
      :today="sampleToday"
      tracking
      :kinds="sampleKinds"
      :workflow="feedWorkflow"
      :hidden-stages="['skipped', 'rejected']"
      :regions="feedRegions"
      :quick-stages="sampleQuickStages"
      can-track
      can-propose
      :proposal="{ privateCatalog: true }"
      :page-size="12"
      :attention="attention"
      :event-href="(id) => `#${id}`"
      @open="(id) => (last = `open ${id}`)"
      @tracked="(tracking) => (last = `track ${tracking.eventId} → ${tracking.stage}`)"
      @proposed="(event) => (last = `proposed ${event.id}`)"
    >
      <template #attention="{ close }">
        <section class="space-y-2 rounded-lg border border-border p-4" data-test="past-strip">
          <h2 class="text-base font-semibold">Past events to close</h2>
          <ul class="space-y-1 text-sm">
            <li v-for="event in pastEvents" :key="event.id">
              {{ eventTitle(event.facts, 'en') }} · {{ formatEventWhen(event.facts.starts, 'en') }}
            </li>
          </ul>
          <button type="button" class="text-sm underline underline-offset-4" @click="close">Close</button>
        </section>
      </template>
    </EventsFeed>
  </main>
</template>
