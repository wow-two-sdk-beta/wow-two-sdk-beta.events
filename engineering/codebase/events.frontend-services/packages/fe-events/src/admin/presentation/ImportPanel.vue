<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { computed, shallowRef } from 'vue';
import type { EventImportBundle, EventImportMapEntry, EventImportReport } from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import type { EventsFailure } from '../../kit/client/EventsFailure';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';

/**
 * Imports a `wow2.events-import.v1` bundle file into a catalog: the reviewer picks the file, runs a dry run, reads the
 * report, then imports the same file after a confirmation. Needs `events:admin`.
 */
defineOptions({ name: 'ImportPanel' });

const props = withDefaults(defineProps<{ client: EventsClient; catalog: string; locale?: EventsLocale }>(), {
  locale: 'en',
});

const importFormat = 'wow2.events-import.v1';
const bundle = shallowRef<EventImportBundle | null>(null);
const fileError = shallowRef<'importInvalidJson' | 'importWrongFormat' | null>(null);
const dryReport = shallowRef<EventImportReport | null>(null);
const finalReport = shallowRef<EventImportReport | null>(null);
const failure = shallowRef<EventsFailure | null>(null);
const pending = shallowRef(false);
const dryRunOk = shallowRef(false);

const report = computed(() => finalReport.value ?? dryReport.value);
const rows = computed<readonly EventImportMapEntry[]>(() =>
  [...(report.value?.map ?? [])].sort(
    (left, right) => Number(right.outcome === 'failed') - Number(left.outcome === 'failed'),
  ),
);
const counts = computed(() =>
  report.value
    ? ([
        ['created', 'importCreated', report.value.created],
        ['updated', 'importUpdated', report.value.updated],
        ['unchanged', 'importUnchanged', report.value.unchanged],
        ['failed', 'importFailed', report.value.failed],
      ] as const)
    : [],
);

function reset() {
  bundle.value = null;
  fileError.value = null;
  dryReport.value = null;
  finalReport.value = null;
  failure.value = null;
  dryRunOk.value = false;
}

async function onFile(event: Event) {
  reset();
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  let parsed: unknown;
  try {
    parsed = JSON.parse(await file.text());
  } catch {
    fileError.value = 'importInvalidJson';
    return;
  }
  const candidate = parsed as Partial<EventImportBundle> | null;
  if (
    typeof candidate !== 'object' ||
    candidate === null ||
    candidate.format !== importFormat ||
    !Array.isArray(candidate.rows)
  ) {
    fileError.value = 'importWrongFormat';
    return;
  }
  bundle.value = candidate as EventImportBundle;
}

async function run(dryRun: boolean) {
  if (!bundle.value) return;
  pending.value = true;
  failure.value = null;
  const result = await props.client.importEvents(props.catalog, bundle.value, dryRun);
  pending.value = false;
  if (!result.ok) {
    failure.value = result.failure;
    if (dryRun) dryRunOk.value = false;
    return;
  }
  if (dryRun) {
    dryReport.value = result.value;
    finalReport.value = null;
    dryRunOk.value = true;
  } else {
    finalReport.value = result.value;
    dryRunOk.value = false;
  }
}

function runImport() {
  if (!bundle.value || !dryRunOk.value) return;
  const message = eventsText('importConfirm', props.locale, {
    count: bundle.value.rows.length,
    source: bundle.value.source,
    catalog: props.catalog,
  });
  if (window.confirm(message)) void run(false);
}
</script>

<template>
  <section class="space-y-4" aria-labelledby="import-title">
    <h2 id="import-title" class="text-lg font-semibold">{{ eventsText('imports', locale) }}</h2>
    <label class="block space-y-1 text-sm">
      <span class="text-muted-foreground">{{ eventsText('importFile', locale) }}</span>
      <input
        type="file"
        accept="application/json"
        class="block text-sm"
        data-test="import-file"
        @change="onFile"
      />
    </label>
    <p v-if="fileError" class="text-sm text-destructive" role="alert" data-test="import-file-error">
      {{ eventsText(fileError, locale) }}
    </p>
    <dl v-if="bundle" class="flex flex-wrap gap-x-6 gap-y-1 text-sm" data-test="import-bundle">
      <div>
        <dt class="inline text-muted-foreground">{{ eventsText('importSource', locale) }}:</dt>
        <dd class="inline">{{ bundle.source }}</dd>
      </div>
      <div>
        <dt class="inline text-muted-foreground">{{ eventsText('importRows', locale) }}:</dt>
        <dd class="inline">{{ bundle.rows.length }}</dd>
      </div>
    </dl>
    <div class="flex gap-2">
      <Button size="sm" variant="outline" :is-loading="pending" :is-disabled="!bundle" data-test="import-dry-run" @click="run(true)">
        {{ eventsText('dryRun', locale) }}
      </Button>
      <Button size="sm" :is-disabled="!dryRunOk || pending" data-test="import-run" @click="runImport">
        {{ eventsText('importRun', locale) }}
      </Button>
    </div>
    <FailureNotice v-if="failure" :failure="failure" :locale="locale" context="admin" />
    <template v-if="report">
      <h3 class="font-semibold">
        {{ eventsText(report.dryRun ? 'importDryReport' : 'importDoneReport', locale) }}
      </h3>
      <dl class="grid gap-3 sm:grid-cols-4">
        <div v-for="[key, label, value] in counts" :key="key" class="rounded-md border border-border p-3">
          <dt class="text-xs text-muted-foreground">{{ eventsText(label, locale) }}</dt>
          <dd class="text-2xl font-semibold" :data-test="`import-count-${key}`">{{ value }}</dd>
        </div>
      </dl>
      <table class="w-full text-left text-sm" data-test="import-map">
        <thead class="text-xs text-muted-foreground">
          <tr>
            <th class="py-1 pr-3">{{ eventsText('legacyId', locale) }}</th>
            <th class="py-1 pr-3">{{ eventsText('outcome', locale) }}</th>
            <th class="py-1 pr-3">{{ eventsText('eventId', locale) }}</th>
            <th class="py-1">{{ eventsText('rowError', locale) }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in rows"
            :key="row.legacyId"
            class="border-t border-border"
            :class="{ 'text-destructive': row.outcome === 'failed' }"
            data-test="import-row"
          >
            <td class="py-1 pr-3 font-mono text-xs">{{ row.legacyId }}</td>
            <td class="py-1 pr-3">{{ row.outcome }}</td>
            <td class="py-1 pr-3 font-mono text-xs">{{ row.eventId }}</td>
            <td class="py-1">{{ row.error ?? '' }}</td>
          </tr>
        </tbody>
      </table>
    </template>
  </section>
</template>
