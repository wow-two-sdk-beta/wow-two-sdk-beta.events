<script setup lang="ts">
import { Button, RefreshButton } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { onMounted, shallowRef, useTemplateRef, watch } from 'vue';
import { DigestChannelConstants, type DigestModel, type DigestSpec } from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import { useEventsRequest } from '../../kit/hooks/UseEventsRequest';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';
import { formatInZone } from '../digests/DigestDraft';
import DigestEditor from './DigestEditor.vue';
import DigestRunHistory from './DigestRunHistory.vue';

/**
 * Manages the caller's organization's digests of one catalog (`events:admin`): the list with each next run in its own
 * zone, the editor of the chosen or a new digest, and its run history. The console's digests page and a product's
 * settings page (Ocharo's Telegram delivery, catalog `ocharo-fashion`) embed the same panel.
 */
defineOptions({ name: 'DigestsPanel' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    locale?: EventsLocale;
    /** A new digest's fields, such as the product's default schedule and recipients. */
    defaults?: Partial<DigestSpec>;
    /** The channels to offer. */
    channels?: readonly string[];
    /** Fixes each event's link to the product's own page. */
    linkTemplate?: string | null;
  }>(),
  { locale: 'en', defaults: () => ({}), channels: () => [DigestChannelConstants.telegram], linkTemplate: null },
);

const list = useEventsRequest((signal) => props.client.listDigests(props.catalog, signal));
const selected = shallowRef<string | null>(null);
const creating = shallowRef(false);
const history = useTemplateRef<InstanceType<typeof DigestRunHistory>>('history');

async function load() {
  const digests = await list.run();
  if (!digests) return;
  if (selected.value && !digests.some((digest) => digest.id === selected.value)) selected.value = null;
  if (!selected.value && !creating.value && digests.length > 0) selected.value = digests[0]!.id;
  if (digests.length === 0) creating.value = true;
}

function startNew() {
  selected.value = null;
  creating.value = true;
}

function choose(digest: DigestModel) {
  creating.value = false;
  selected.value = digest.id;
}

async function onSaved(digest: DigestModel) {
  creating.value = false;
  selected.value = digest.id;
  await list.run();
}

async function onDeleted() {
  selected.value = null;
  creating.value = false;
  await load();
}

async function onSent() {
  history.value?.reload();
  await list.run();
}

onMounted(load);
watch([() => props.catalog, () => props.client], () => {
  selected.value = null;
  creating.value = false;
  void load();
});
</script>

<template>
  <section class="space-y-4" aria-labelledby="digests-title" data-test="digests-panel">
    <header class="flex flex-wrap items-center justify-between gap-2">
      <h2 id="digests-title" class="text-lg font-semibold">{{ eventsText('digests', locale) }}</h2>
      <div class="flex gap-2">
        <RefreshButton size="sm" :is-refreshing="list.pending.value" @click="load" />
        <Button size="sm" data-test="digest-new" @click="startNew">{{ eventsText('digestNew', locale) }}</Button>
      </div>
    </header>

    <div v-if="list.pending.value && !list.value.value" class="space-y-2" aria-hidden="true">
      <SkeletonState v-for="index in 2" :key="index" class="h-12 w-full" />
    </div>
    <FailureNotice
      v-else-if="list.failure.value"
      :failure="list.failure.value"
      :locale="locale"
      context="admin"
      retry
      @retry="load"
    />
    <template v-else>
      <p v-if="!list.value.value?.length" class="text-sm text-muted-foreground">
        {{ eventsText('digestNone', locale) }}
      </p>
      <ul v-else class="divide-y divide-border rounded-md border border-border" data-test="digest-list">
        <li v-for="digest in list.value.value" :key="digest.id">
          <button
            type="button"
            class="flex w-full flex-wrap items-center gap-2 px-3 py-2 text-left text-sm"
            :class="{ 'bg-muted/40 font-semibold': digest.id === selected }"
            data-test="digest-item"
            @click="choose(digest)"
          >
            <span>{{ digest.name }}</span>
            <Badge v-if="!digest.enabled" size="sm" :variant="BadgeVariant.Neutral">
              {{
                eventsText('digestOff', locale)
              }}
            </Badge>
            <span v-if="digest.enabled && digest.nextRunAtUtc" class="ml-auto text-muted-foreground">
              {{ eventsText('digestNextRun', locale) }}:
              {{ formatInZone(digest.nextRunAtUtc, digest.schedule.timeZone, locale) }}
            </span>
          </button>
        </li>
      </ul>
    </template>

    <DigestEditor
      v-if="selected || creating"
      :key="selected ?? 'new'"
      :client="client"
      :catalog="catalog"
      :digest-id="selected"
      :locale="locale"
      :defaults="defaults"
      :channels="channels"
      :link-template="linkTemplate"
      @saved="onSaved"
      @deleted="onDeleted"
      @sent="onSent"
    />
    <DigestRunHistory
      v-if="selected"
      ref="history"
      :key="`runs-${selected}`"
      :client="client"
      :catalog="catalog"
      :digest-id="selected"
      :locale="locale"
    />
  </section>
</template>
