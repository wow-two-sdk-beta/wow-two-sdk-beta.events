<script setup lang="ts">
import { ReviewQueue } from '@wow-two-beta/fe-events/admin';
import { useConsole } from '../../../application/console/UseConsole';

/** Renders the review queue of the chosen catalog: candidates and pending changes with their evidence. */
defineOptions({ name: 'ReviewPage' });

const app = useConsole();
</script>

<template>
  <ReviewQueue
    v-if="app.catalog.value"
    :key="app.catalog.value.slug"
    :client="app.client"
    :catalog="app.catalog.value.slug"
    :kinds="app.catalog.value.kinds"
    :locale="app.locale.value"
  />
</template>
