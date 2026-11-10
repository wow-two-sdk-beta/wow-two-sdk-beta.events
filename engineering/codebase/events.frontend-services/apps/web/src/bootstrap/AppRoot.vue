<script setup lang="ts">
import { RouteAnnouncer } from '@wow-two-beta/fe/router';
import { RouterView } from 'vue-router';
import { provideConsole } from '../application/console/UseConsole';
import ConsoleShell from '../presentation/console/components/ConsoleShell.vue';

/** Composes the Events console: the shared session and client, the frame, and one route per screen. */
defineOptions({ name: 'AppRoot' });

provideConsole();
</script>

<template>
  <RouteAnnouncer />
  <ConsoleShell>
    <RouterView />
  </ConsoleShell>
</template>
