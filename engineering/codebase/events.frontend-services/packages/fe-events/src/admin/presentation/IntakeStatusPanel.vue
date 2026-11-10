<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { computed, onMounted, watch } from 'vue';
import type { IntakeStatusModel } from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import { formatTimestamp } from '../../kit/dates/EventDates';
import { useEventsRequest } from '../../kit/hooks/UseEventsRequest';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';

/**
 * Shows the SCFD pull: each source's cursor, last poll, newest record, counts and last error, plus what waits for a
 * reviewer; operators poll on demand. Needs `events:admin`.
 */
defineOptions({ name: 'IntakeStatusPanel' });

const props = withDefaults(defineProps<{ client: EventsClient; locale?: EventsLocale }>(), { locale: 'en' });

const status = useEventsRequest((signal) => props.client.getIntakeStatus(signal));
const poll = useEventsRequest((signal) => props.client.pollIntake(signal));
const current = computed<IntakeStatusModel | null>(() => poll.value.value ?? status.value.value);

async function pollNow() {
  const result = await poll.run();
  if (result) status.value.value = result;
  poll.value.value = null;
}

onMounted(() => status.run());
watch(
  () => props.client,
  () => status.run(),
);
</script>

<template>
  <section class="space-y-4" aria-labelledby="intake-title">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h2 id="intake-title" class="text-lg font-semibold">{{ eventsText('intake', locale) }}</h2>
      <div class="flex gap-2">
        <Button size="sm" variant="ghost" :is-loading="status.pending.value" @click="status.run()">
          {{ eventsText('refresh', locale) }}
        </Button>
        <Button size="sm" variant="outline" :is-loading="poll.pending.value" :is-disabled="!current" @click="pollNow">
          {{ eventsText('pollNow', locale) }}
        </Button>
      </div>
    </div>
    <FailureNotice v-if="poll.failure.value" :failure="poll.failure.value" :locale="locale" context="admin" />
    <div v-if="status.pending.value && !current" class="space-y-2" aria-hidden="true">
      <SkeletonState class="h-16 w-full" />
      <SkeletonState class="h-32 w-full" />
    </div>
    <FailureNotice
      v-else-if="status.failure.value && !current"
      :failure="status.failure.value"
      :locale="locale"
      context="admin"
      retry
      @retry="status.run()"
    />
    <template v-else-if="current">
      <dl class="grid gap-3 sm:grid-cols-3">
        <div class="rounded-md border border-border p-3">
          <dt class="text-xs text-muted-foreground">{{ eventsText('candidates', locale) }}</dt>
          <dd class="text-2xl font-semibold" data-test="intake-candidates">{{ current.candidates }}</dd>
        </div>
        <div class="rounded-md border border-border p-3">
          <dt class="text-xs text-muted-foreground">{{ eventsText('pendingChangesTotal', locale) }}</dt>
          <dd class="text-2xl font-semibold">{{ current.pendingChanges }}</dd>
        </div>
        <div class="rounded-md border border-border p-3">
          <dt class="text-xs text-muted-foreground">{{ eventsText('oldestUnresolved', locale) }}</dt>
          <dd class="text-sm font-medium">{{ formatTimestamp(current.oldestUnresolvedAtUtc, locale) }}</dd>
        </div>
      </dl>
      <p v-if="!current.sources.length" class="text-sm text-muted-foreground">{{ eventsText('noSources', locale) }}</p>
      <article
        v-for="source in current.sources"
        :key="source.name"
        class="space-y-2 rounded-md border border-border p-3 text-sm"
        :class="{ 'border-destructive/50': source.lastError }"
        data-test="intake-source"
      >
        <div class="flex flex-wrap items-center gap-2">
          <h3 class="font-semibold">{{ source.name }}</h3>
          <Badge size="sm" :variant="source.enabled ? BadgeVariant.Success : BadgeVariant.Neutral">
            {{ source.enabled ? eventsText('enabled', locale) : eventsText('disabled', locale) }}
          </Badge>
          <span class="text-xs text-muted-foreground">{{ source.catalog }} · {{ eventsText('subscription', locale) }} {{ source.subscriptionId }}</span>
        </div>
        <dl class="grid gap-x-4 gap-y-1 text-xs sm:grid-cols-3">
          <div>
            <dt class="inline text-muted-foreground">{{ eventsText('lastPolled', locale) }}:</dt>
            <dd class="inline">{{ formatTimestamp(source.lastPolledAtUtc, locale) }}</dd>
          </div>
          <div>
            <dt class="inline text-muted-foreground">{{ eventsText('lastReceived', locale) }}:</dt>
            <dd class="inline">{{ formatTimestamp(source.lastReceivedAtUtc, locale) }}</dd>
          </div>
          <div>
            <dt class="inline text-muted-foreground">{{ eventsText('newestObserved', locale) }}:</dt>
            <dd class="inline">{{ formatTimestamp(source.newestObservedAtUtc, locale) }}</dd>
          </div>
          <div>
            <dt class="inline text-muted-foreground">{{ eventsText('received', locale) }}:</dt>
            <dd class="inline">{{ source.received ?? 0 }}</dd>
          </div>
          <div>
            <dt class="inline text-muted-foreground">{{ eventsText('quarantined', locale) }}:</dt>
            <dd class="inline">{{ source.quarantined ?? 0 }}</dd>
          </div>
          <div>
            <dt class="inline text-muted-foreground">{{ eventsText('failures', locale) }}:</dt>
            <dd class="inline">{{ source.consecutiveFailures ?? 0 }}</dd>
          </div>
        </dl>
        <p v-if="source.cursor" class="break-all font-mono text-xs text-muted-foreground">cursor {{ source.cursor }}</p>
        <p v-if="source.lastError" class="text-xs text-destructive" role="alert">
          {{ eventsText('lastError', locale) }}: {{ source.lastError }}
        </p>
      </article>
    </template>
  </section>
</template>
