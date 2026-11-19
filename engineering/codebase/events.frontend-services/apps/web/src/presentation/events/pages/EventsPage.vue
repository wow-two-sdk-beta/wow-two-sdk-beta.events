<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { CatalogEventsTable, EventSourcesPanel } from '@wow-two-beta/fe-events/admin';
import type { EventModel } from '@wow-two-beta/fe-events/contracts';
import { EventDetail, eventsText } from '@wow-two-beta/fe-events/kit';
import { shallowRef, watch } from 'vue';
import { useConsole } from '../../../application/console/UseConsole';

/**
 * Renders every event of the chosen catalog in every review state; opening one shows its detail above the table, with
 * its sources and the split that takes one of them off into a candidate of its own.
 */
defineOptions({ name: 'EventsPage' });

const app = useConsole();
const opened = shallowRef<EventModel | null>(null);
const version = shallowRef(0);

watch(
  () => app.catalog.value?.slug,
  () => {
    opened.value = null;
  },
);

/** Reads the opened event again after a split or a conflict, and reloads the table and the detail. */
async function reload() {
  const slug = app.catalog.value?.slug;
  if (slug && opened.value) {
    const result = await app.client.getEvent(slug, opened.value.id);
    opened.value = result.ok ? result.value : null;
  }
  version.value += 1;
}
</script>

<template>
  <div v-if="app.catalog.value" class="space-y-6">
    <section v-if="opened" class="space-y-3 rounded-lg border border-border p-4">
      <div class="flex justify-end">
        <Button size="sm" variant="ghost" @click="opened = null">{{ eventsText('cancel', app.locale.value) }}</Button>
      </div>
      <EventDetail
        :key="`${opened.id}-${version}`"
        :client="app.client"
        :catalog="app.catalog.value.slug"
        :event-id="opened.id"
        :kinds="app.catalog.value.kinds"
        :locale="app.locale.value"
      />
      <EventSourcesPanel
        :client="app.client"
        :catalog="app.catalog.value.slug"
        :event="opened"
        :locale="app.locale.value"
        @split="reload"
        @stale="reload"
      />
    </section>
    <CatalogEventsTable
      :key="`${app.catalog.value.slug}-${version}`"
      :client="app.client"
      :catalog="app.catalog.value.slug"
      :kinds="app.catalog.value.kinds"
      :locale="app.locale.value"
      @open="(_id, event) => (opened = event)"
    />
  </div>
</template>
