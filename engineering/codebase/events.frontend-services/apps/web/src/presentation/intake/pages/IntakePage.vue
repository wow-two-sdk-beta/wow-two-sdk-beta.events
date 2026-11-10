<script setup lang="ts">
import { IntakeStatusPanel } from '@wow-two-beta/fe-events/admin';
import { useConsole } from '../../../application/console/UseConsole';

/** Renders the SCFD intake status for operators (`events:admin`). */
defineOptions({ name: 'IntakePage' });

const app = useConsole();
</script>

<template>
  <IntakeStatusPanel :client="app.client" :locale="app.locale.value" />
</template>
