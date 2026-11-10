<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { CatalogEventsTable } from '@wow-two-beta/fe-events/admin';
import { EventDetail, eventsText } from '@wow-two-beta/fe-events/kit';
import { shallowRef, watch } from 'vue';
import { useConsole } from '../../../application/console/UseConsole';

/** Renders every event of the chosen catalog in every review state; opening one shows its detail above the table. */
defineOptions({ name: 'EventsPage' });

const app = useConsole();
const opened = shallowRef<string | null>(null);

watch(
  () => app.catalog.value?.slug,
  () => {
    opened.value = null;
  },
);
</script>

<template>
  <div v-if="app.catalog.value" class="space-y-6">
    <section v-if="opened" class="space-y-3 rounded-lg border border-border p-4">
      <div class="flex justify-end">
        <Button size="sm" variant="ghost" @click="opened = null">{{ eventsText('cancel', app.locale.value) }}</Button>
      </div>
      <EventDetail
        :key="opened"
        :client="app.client"
        :catalog="app.catalog.value.slug"
        :event-id="opened"
        :kinds="app.catalog.value.kinds"
        :locale="app.locale.value"
      />
    </section>
    <CatalogEventsTable
      :key="app.catalog.value.slug"
      :client="app.client"
      :catalog="app.catalog.value.slug"
      :kinds="app.catalog.value.kinds"
      :locale="app.locale.value"
      @open="(id) => (opened = id)"
    />
  </div>
</template>
