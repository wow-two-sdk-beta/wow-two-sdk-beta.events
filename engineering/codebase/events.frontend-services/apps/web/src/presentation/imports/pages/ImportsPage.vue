<script setup lang="ts">
import { ImportPanel } from '@wow-two-beta/fe-events/admin';
import { useConsole } from '../../../application/console/UseConsole';

/** Renders the import screen of the chosen catalog: a dry run of a bundle file, then the real import. */
defineOptions({ name: 'ImportsPage' });

const app = useConsole();
</script>

<template>
  <ImportPanel
    v-if="app.catalog.value"
    :key="app.catalog.value.slug"
    :client="app.client"
    :catalog="app.catalog.value.slug"
    :locale="app.locale.value"
  />
</template>
