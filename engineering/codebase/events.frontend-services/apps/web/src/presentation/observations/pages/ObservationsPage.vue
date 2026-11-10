<script setup lang="ts">
import { ObservationsList } from '@wow-two-beta/fe-events/admin';
import { useConsole } from '../../../application/console/UseConsole';

/** Renders the chosen catalog's observations, filterable by state, quarantined first after all. */
defineOptions({ name: 'ObservationsPage' });

const app = useConsole();
</script>

<template>
  <ObservationsList
    v-if="app.catalog.value"
    :key="app.catalog.value.slug"
    :client="app.client"
    :catalog="app.catalog.value.slug"
    :locale="app.locale.value"
  />
</template>
