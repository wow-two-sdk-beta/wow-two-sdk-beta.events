<script setup lang="ts">
import { DigestsPanel } from '@wow-two-beta/fe-events/admin';
import { DigestChannelConstants } from '@wow-two-beta/fe-events/contracts';
import { useConsole } from '../../../application/console/UseConsole';

/** Renders the chosen catalog's digests for the signed-in organization: the list, the editor and the run history. */
defineOptions({ name: 'DigestsPage' });

const app = useConsole();
const channels = [DigestChannelConstants.telegram, DigestChannelConstants.log];
</script>

<template>
  <DigestsPanel
    v-if="app.catalog.value"
    :key="app.catalog.value.slug"
    :client="app.client"
    :catalog="app.catalog.value.slug"
    :locale="app.locale.value"
    :channels="channels"
  />
</template>
