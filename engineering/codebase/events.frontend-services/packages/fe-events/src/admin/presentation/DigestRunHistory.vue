<script setup lang="ts">
import { Button, RefreshButton } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { onMounted, shallowRef, watch } from 'vue';
import type { DigestRunModel, DigestRunStatus } from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import { formatTimestamp } from '../../kit/dates/EventDates';
import { useEventsRequest } from '../../kit/hooks/UseEventsRequest';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../../kit/text/EventsMessages';
import { countedRecipientStatuses, recipientStatusKeys } from '../digests/RecipientStatus';
import { TelegramText } from '../digests/TelegramText';
import DigestRecipients from './DigestRecipients.vue';

/**
 * Lists a digest's runs, newest first: when each ran, what started it, how many events went out and how many recipients
 * were reached, had no Telegram link, had it muted or failed; a run opens to its messages and notes.
 */
defineOptions({ name: 'DigestRunHistory' });

const props = withDefaults(
  defineProps<{ client: EventsClient; catalog: string; digestId: string; locale?: EventsLocale }>(),
  { locale: 'en' },
);

const request = useEventsRequest((signal, cursor: string | undefined) =>
  props.client.listDigestRuns(props.catalog, props.digestId, cursor ? { cursor } : {}, signal),
);
const runs = shallowRef<readonly DigestRunModel[]>([]);
const next = shallowRef<string | null>(null);
const open = shallowRef<string | null>(null);

async function load(more = false) {
  const page = await request.run(more ? (next.value ?? undefined) : undefined);
  if (!page) return;
  runs.value = more ? [...runs.value, ...page.items] : page.items;
  next.value = page.nextCursor ?? null;
}

const statusKeys: Readonly<Record<DigestRunStatus, EventsMessageKey>> = {
  running: 'runRunning',
  completed: 'runCompleted',
  partial: 'runPartial',
  failed: 'runFailed',
  empty: 'runEmpty',
  missed: 'runMissed',
};
const statusVariants: Readonly<Record<DigestRunStatus, BadgeVariant>> = {
  running: BadgeVariant.Info,
  completed: BadgeVariant.Success,
  partial: BadgeVariant.Warning,
  failed: BadgeVariant.Danger,
  empty: BadgeVariant.Neutral,
  missed: BadgeVariant.Warning,
};
const count = (run: DigestRunModel, status: string) =>
  (run.recipients ?? []).filter((item) => item.status === status).length;
/** Writes a run's recipient tally in plain words: "Delivered 1 · No Telegram link 0 · Muted 0 · Failed 0". */
const tally = (run: DigestRunModel) =>
  countedRecipientStatuses
    .map((status) => `${eventsText(recipientStatusKeys[status], props.locale)} ${count(run, status)}`)
    .join(' · ');

defineExpose({ reload: () => load() });
onMounted(() => load());
watch([() => props.catalog, () => props.digestId, () => props.client], () => load());
</script>

<template>
  <section class="space-y-3" aria-labelledby="digest-runs-title" data-test="digest-runs">
    <header class="flex items-center justify-between gap-2">
      <h3 id="digest-runs-title" class="text-sm font-semibold">{{ eventsText('digestRuns', locale) }}</h3>
      <RefreshButton size="md" :is-refreshing="request.pending.value" @click="load()" />
    </header>
    <div v-if="request.pending.value && runs.length === 0" class="space-y-2" aria-hidden="true">
      <SkeletonState v-for="index in 2" :key="index" class="h-10 w-full" />
    </div>
    <FailureNotice
      v-else-if="request.failure.value"
      :failure="request.failure.value"
      :locale="locale"
      context="admin"
      retry
      @retry="load()"
    />
    <p v-else-if="runs.length === 0" class="text-sm text-muted-foreground">{{ eventsText('digestNoRuns', locale) }}</p>
    <ol v-else class="space-y-2">
      <li
        v-for="run in runs"
        :key="run.id"
        class="rounded-md border border-border p-3"
        data-test="digest-run"
        :data-status="run.status"
      >
        <button
          type="button"
          class="flex min-h-10 w-full flex-wrap items-center gap-2 text-left text-sm"
          :aria-expanded="open === run.id"
          @click="open = open === run.id ? null : run.id"
        >
          <time :datetime="run.startedAtUtc" class="font-medium">{{ formatTimestamp(run.startedAtUtc, locale) }}</time>
          <span class="text-muted-foreground">{{
            eventsText(run.trigger === 'manual' ? 'triggerManual' : 'triggerSchedule', locale)
          }}</span>
          <Badge :variant="statusVariants[run.status] ?? BadgeVariant.Neutral">
            {{ statusKeys[run.status] ? eventsText(statusKeys[run.status], locale) : run.status }}
          </Badge>
          <span class="text-muted-foreground">
            {{ eventsText('digestMatched', locale, { selected: run.selected, matched: run.matched }) }}
          </span>
          <span class="ml-auto text-xs text-muted-foreground" data-test="digest-run-counts">{{ tally(run) }}</span>
        </button>
        <div v-if="open === run.id" class="mt-3 space-y-3">
          <DigestRecipients :recipients="run.recipients ?? []" :locale="locale" />
          <TelegramText v-for="text in run.texts ?? []" :key="text.language" :text="text.text" />
          <ul v-if="run.notes?.length" class="list-disc pl-5 text-sm text-muted-foreground">
            <li v-for="note in run.notes" :key="note">{{ note }}</li>
          </ul>
        </div>
      </li>
    </ol>
    <Button v-if="next" size="md" variant="outline" :is-loading="request.pending.value" @click="load(true)">
      {{ eventsText('digestLoadMore', locale) }}
    </Button>
  </section>
</template>
