<script setup lang="ts">
import { useConfirmation } from '@wow-two-beta/fe/foundation/hooks';
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import {
  AlertModal,
  AlertModalAction,
  AlertModalCancel,
  AlertModalContent,
  ModalDescription,
  ModalFooter,
  ModalHeader,
  ModalTitle,
} from '@wow-two-beta/fe/presentation/overlays';
import { computed, onMounted, shallowRef, watch } from 'vue';
import {
  DigestChannelConstants,
  DigestDayValues,
  DigestLanguageValues,
  EventStatusValues,
  type DigestDay,
  type DigestModel,
  type DigestPreviewModel,
  type DigestRunModel,
  type DigestSpec,
  type EventStatus,
  type WorkflowModel,
  type WorkflowStageModel,
} from '../../contracts';
import type { EventsClient } from '../../kit/client/CreateEventsClient';
import FailureNotice from '../../kit/presentation/FailureNotice.vue';
import { useEventsRequest } from '../../kit/hooks/UseEventsRequest';
import { EventsLabels } from '../../kit/text/EventsLabels';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../../kit/text/EventsMessages';
import {
  defaultDigestSpec,
  digestDraft,
  digestLanguageOf,
  digestSpec,
  formatInZone,
  type DigestDraft,
  type DigestDraftProblem,
} from '../digests/DigestDraft';
import { TelegramText } from '../digests/TelegramText';
import DigestRecipients from './DigestRecipients.vue';

/**
 * Edits one digest of a catalog for the caller's organization (`events:admin`): its schedule in an IANA zone, which
 * events go out, to whom, in which language and through which channel; a saved digest shows its next run in its own
 * zone, previews the messages a send now would send, and sends now. A product embeds it for its catalog, passing its
 * own defaults (a new digest's fields) and fixing the channel or the link template it owns. The stage pickers offer the
 * organization's workflow: the `workflow` prop, else the one the client reads; when neither is at hand they keep the
 * stages the digest already names, without a failure notice (a product's mirror may not forward the workflow route).
 */
defineOptions({ name: 'DigestEditor' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    /** The digest to edit; none creates one. */
    digestId?: string | null;
    locale?: EventsLocale;
    /** A new digest's fields; Monday 09:00 in Tashkent, new and upcoming events, in Uzbek, when none. */
    defaults?: Partial<DigestSpec>;
    /** The channels to offer; the editor hides the choice when there is one. */
    channels?: readonly string[];
    /** Fixes each event's link to the product's own page; the field is hidden when set. */
    linkTemplate?: string | null;
    /** The organization's stages for the stage pickers; none reads its workflow through the client. */
    workflow?: Pick<WorkflowModel, 'stages' | 'defaultStage'> | null;
  }>(),
  {
    digestId: null,
    locale: 'en',
    defaults: () => ({}),
    channels: () => [DigestChannelConstants.telegram],
    linkTemplate: null,
    workflow: null,
  },
);

const emit = defineEmits<{
  saved: [digest: DigestModel];
  deleted: [digest: DigestModel];
  sent: [run: DigestRunModel];
}>();

const load = useEventsRequest((signal, id: string) => props.client.getDigest(props.catalog, id, signal));
const save = useEventsRequest((signal, spec: DigestSpec, id: string | null) =>
  id
    ? props.client.updateDigest(props.catalog, id, spec, signal)
    : props.client.createDigest(props.catalog, spec, signal),
);
const remove = useEventsRequest((signal, id: string, revision: number) =>
  props.client.deleteDigest(props.catalog, id, revision, signal),
);
const preview = useEventsRequest((signal, id: string) => props.client.previewDigest(props.catalog, id, signal));
const send = useEventsRequest((signal, id: string) => props.client.sendDigest(props.catalog, id, signal));
/** Reads the organization's workflow for the stage pickers; its failure stays silent. */
const workflowRead = useEventsRequest((signal) => props.client.getWorkflow(signal));

const digest = shallowRef<DigestModel | null>(null);
const draft = shallowRef<DigestDraft>(newDraft());
const problem = shallowRef<DigestDraftProblem | null>(null);
const savedNotice = shallowRef(false);
const confirmingDelete = shallowRef(false);
const lastRun = shallowRef<DigestRunModel | null>(null);
const shownPreview = shallowRef<DigestPreviewModel | null>(null);
/** Asks the send-now confirmation in the app's own dialog; the request carries the recipient count. */
const {
  request: sendRequest,
  confirm: confirmSend,
  accept: acceptSend,
  cancel: cancelSend,
} = useConfirmation<number>();
/** Keeps the count the dialog shows while it closes, after the request is cleared. */
const sendCount = shallowRef(0);

const failure = computed(
  () => load.failure.value ?? save.failure.value ?? remove.failure.value ?? preview.failure.value ?? send.failure.value,
);
const busy = computed(() => save.pending.value || remove.pending.value || send.pending.value);
const nextRun = computed(() =>
  digest.value?.nextRunAtUtc
    ? formatInZone(digest.value.nextRunAtUtc, digest.value.schedule.timeZone, props.locale)
    : null,
);
const problemText = computed(() =>
  problem.value
    ? eventsText(problem.value.key, props.locale, 'line' in problem.value ? { line: problem.value.line } : {})
    : null,
);
const showChannels = computed(() => props.channels.length > 1);
const workflow = computed(() => props.workflow ?? workflowRead.value.value);
/** Offers the workflow's stages, then any key the digest names that the workflow no longer has. */
const stageOptions = computed<readonly WorkflowStageModel[]>(() => {
  const known = workflow.value?.stages ?? [];
  const named = [...new Set([...draft.value.stages, ...draft.value.excludeStages])]
    .filter((key) => !known.some((stage) => stage.key === key))
    .map((key) => ({ key }));
  return [...known, ...named];
});
const defaultStageLabel = computed(() =>
  workflow.value?.defaultStage
    ? EventsLabels.stage(workflow.value.defaultStage, workflow.value.stages, props.locale)
    : null,
);

function newDraft(): DigestDraft {
  const spec: DigestSpec = {
    ...defaultDigestSpec,
    language: digestLanguageOf(props.locale),
    channel: props.channels[0] ?? DigestChannelConstants.telegram,
    ...props.defaults,
  };
  return digestDraft(props.linkTemplate ? { ...spec, linkTemplate: props.linkTemplate } : spec);
}

async function reload() {
  savedNotice.value = false;
  confirmingDelete.value = false;
  problem.value = null;
  lastRun.value = null;
  shownPreview.value = null;
  if (!props.digestId) {
    digest.value = null;
    draft.value = newDraft();
    return;
  }
  const current = await load.run(props.digestId);
  if (current) {
    digest.value = current;
    draft.value = digestDraft(current);
  }
}

function set<K extends keyof DigestDraft>(key: K, value: DigestDraft[K]) {
  draft.value = { ...draft.value, [key]: value };
  savedNotice.value = false;
}

function toggle<T>(list: readonly T[], item: T, on: boolean): T[] {
  return on ? [...list.filter((value) => value !== item), item] : list.filter((value) => value !== item);
}

/** Includes or leaves out a stage; a stage is never in both lists, as the service refuses that. */
function setStage(list: 'stages' | 'excludeStages', key: string, on: boolean) {
  const other = list === 'stages' ? 'excludeStages' : 'stages';
  draft.value = {
    ...draft.value,
    [list]: toggle(draft.value[list], key, on),
    [other]: on ? draft.value[other].filter((value) => value !== key) : draft.value[other],
  };
  savedNotice.value = false;
}

function loadWorkflow() {
  if (!props.workflow) void workflowRead.run();
}

async function submit() {
  savedNotice.value = false;
  const written = digestSpec(
    props.linkTemplate ? { ...draft.value, linkTemplate: props.linkTemplate } : draft.value,
    digest.value?.revision ?? null,
  );
  if ('problem' in written) {
    problem.value = written.problem;
    return;
  }
  problem.value = null;
  const result = await save.run(written.spec, digest.value?.id ?? null);
  if (!result) return;
  digest.value = result;
  draft.value = digestDraft(result);
  savedNotice.value = true;
  emit('saved', result);
}

async function deleteDigest() {
  if (!digest.value) return;
  if (!confirmingDelete.value) {
    confirmingDelete.value = true;
    return;
  }
  const removed = await remove.run(digest.value.id, digest.value.revision);
  confirmingDelete.value = false;
  if (removed) emit('deleted', removed);
}

async function showPreview() {
  if (!digest.value) return;
  shownPreview.value = await preview.run(digest.value.id);
}

async function sendNow() {
  if (!digest.value) return;
  const count = shownPreview.value?.recipients.length ?? digest.value.recipients.length;
  sendCount.value = count;
  if (!(await confirmSend(count))) return;
  const run = await send.run(digest.value.id);
  if (!run) return;
  lastRun.value = run;
  emit('sent', run);
}

const dayKey = (day: DigestDay) => `day${day[0]!.toUpperCase()}${day.slice(1)}` as EventsMessageKey;
const channelKey = (channel: string): EventsMessageKey | null =>
  channel === DigestChannelConstants.telegram
    ? 'channelTelegram'
    : channel === DigestChannelConstants.log
      ? 'channelLog'
      : null;
const languageName: Readonly<Record<string, string>> = { 'uz-Latn': 'O‘zbekcha', ru: 'Русский', en: 'English' };
const inputClass = 'rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground';
const checked = (event: Event) => (event.target as HTMLInputElement).checked;
const valueOf = (event: Event) => (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value;

onMounted(() => {
  void reload();
  loadWorkflow();
});
watch([() => props.catalog, () => props.digestId, () => props.client], reload);
watch([() => props.client, () => props.workflow], loadWorkflow);
</script>

<template>
  <section
    class="space-y-4 rounded-lg border border-border p-4"
    aria-labelledby="digest-editor-title"
    data-test="digest-editor"
  >
    <header class="flex flex-wrap items-baseline justify-between gap-2">
      <h2 id="digest-editor-title" class="text-base font-semibold">
        {{ digest ? digest.name : eventsText('digestNew', locale) }}
      </h2>
      <p v-if="digest" class="text-sm text-muted-foreground" data-test="digest-next-run">
        <template v-if="nextRun">{{ eventsText('digestNextRun', locale) }}: {{ nextRun }}</template>
        <template v-else>{{ eventsText('digestOff', locale) }}</template>
      </p>
    </header>

    <form class="grid gap-4" novalidate @submit.prevent="submit">
      <fieldset class="grid gap-4 sm:grid-cols-2" :disabled="busy">
        <label class="flex flex-col gap-1 text-sm">
          <span class="font-medium">{{ eventsText('digestName', locale) }}</span>
          <input
            :class="inputClass"
            name="name"
            maxlength="200"
            :value="draft.name"
            @input="set('name', valueOf($event))"
          />
        </label>
        <label v-if="showChannels" class="flex flex-col gap-1 text-sm">
          <span class="font-medium">{{ eventsText('digestChannel', locale) }}</span>
          <select :class="inputClass" name="channel" :value="draft.channel" @change="set('channel', valueOf($event))">
            <option v-for="channel in channels" :key="channel" :value="channel">
              {{ channelKey(channel) ? eventsText(channelKey(channel)!, locale) : channel }}
            </option>
          </select>
        </label>
        <label class="flex items-center gap-2 text-sm">
          <input type="checkbox" name="enabled" :checked="draft.enabled" @change="set('enabled', checked($event))" />
          <span>{{ eventsText('digestEnabled', locale) }}</span>
        </label>
      </fieldset>

      <fieldset class="grid gap-3" :disabled="busy">
        <legend class="mb-1 text-sm font-medium">{{ eventsText('digestSchedule', locale) }}</legend>
        <div class="flex flex-wrap gap-3 text-sm" role="group" :aria-label="eventsText('digestDays', locale)">
          <label v-for="day in DigestDayValues" :key="day" class="flex items-center gap-1">
            <input
              type="checkbox"
              :name="`day-${day}`"
              :checked="draft.days.includes(day)"
              @change="set('days', toggle(draft.days, day, checked($event)))"
            />
            <span>{{ eventsText(dayKey(day), locale) }}</span>
          </label>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-muted-foreground">{{ eventsText('digestTimes', locale) }}</span>
            <input
              :class="inputClass"
              name="times"
              placeholder="09:00"
              :value="draft.times"
              @input="set('times', valueOf($event))"
            />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-muted-foreground">{{ eventsText('digestTimeZone', locale) }}</span>
            <input
              :class="inputClass"
              name="timeZone"
              placeholder="Asia/Tashkent"
              :value="draft.timeZone"
              @input="set('timeZone', valueOf($event))"
            />
          </label>
        </div>
      </fieldset>

      <fieldset class="grid gap-3" :disabled="busy">
        <legend class="mb-1 text-sm font-medium">{{ eventsText('digestSelection', locale) }}</legend>
        <p class="text-xs text-muted-foreground">{{ eventsText('digestSelectionHint', locale) }}</p>
        <div class="grid gap-3 sm:grid-cols-3">
          <label class="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              name="newSinceLastRun"
              :checked="draft.newSinceLastRun"
              @change="set('newSinceLastRun', checked($event))"
            />
            <span>{{ eventsText('digestNewSinceLastRun', locale) }}</span>
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-muted-foreground">{{ eventsText('digestStartingWithin', locale) }}</span>
            <input
              :class="inputClass"
              type="number"
              min="0"
              name="startingWithinDays"
              :value="draft.startingWithinDays"
              @input="set('startingWithinDays', valueOf($event))"
            />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-muted-foreground">{{ eventsText('digestDeadlineWithin', locale) }}</span>
            <input
              :class="inputClass"
              type="number"
              min="0"
              name="deadlineWithinDays"
              :value="draft.deadlineWithinDays"
              @input="set('deadlineWithinDays', valueOf($event))"
            />
          </label>
        </div>
        <div class="flex flex-wrap gap-3 text-sm" role="group" :aria-label="eventsText('digestStatuses', locale)">
          <span class="text-muted-foreground">{{ eventsText('digestStatuses', locale) }}:</span>
          <label v-for="status in EventStatusValues" :key="status" class="flex items-center gap-1">
            <input
              type="checkbox"
              :name="`status-${status}`"
              :checked="draft.statuses.includes(status)"
              @change="set('statuses', toggle<EventStatus>(draft.statuses, status, checked($event)))"
            />
            <span>{{ EventsLabels.status(status, locale) }}</span>
          </label>
        </div>
        <div class="grid gap-3 sm:grid-cols-2">
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-muted-foreground">{{ eventsText('digestKinds', locale) }}</span>
            <input :class="inputClass" name="kinds" :value="draft.kinds" @input="set('kinds', valueOf($event))" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-muted-foreground">{{ eventsText('digestCities', locale) }}</span>
            <input :class="inputClass" name="cities" :value="draft.cities" @input="set('cities', valueOf($event))" />
          </label>
        </div>
        <label class="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            name="trackedOnly"
            :checked="draft.trackedOnly"
            @change="set('trackedOnly', checked($event))"
          />
          <span>{{ eventsText('digestTrackedOnly', locale) }}</span>
        </label>
        <template v-if="stageOptions.length > 0">
          <div
            class="flex flex-wrap gap-3 text-sm"
            role="group"
            :aria-label="eventsText('digestStages', locale)"
            data-test="digest-stages"
          >
            <span class="text-muted-foreground">{{ eventsText('digestStages', locale) }}:</span>
            <label v-for="stage in stageOptions" :key="stage.key" class="flex items-center gap-1">
              <input
                type="checkbox"
                :name="`stage-${stage.key}`"
                :checked="draft.stages.includes(stage.key)"
                @change="setStage('stages', stage.key, checked($event))"
              />
              <span>{{ EventsLabels.stage(stage.key, stageOptions, locale) }}</span>
            </label>
          </div>
          <div
            class="flex flex-wrap gap-3 text-sm"
            role="group"
            :aria-label="eventsText('digestExcludeStages', locale)"
            data-test="digest-exclude-stages"
          >
            <span class="text-muted-foreground">{{ eventsText('digestExcludeStages', locale) }}:</span>
            <label v-for="stage in stageOptions" :key="stage.key" class="flex items-center gap-1">
              <input
                type="checkbox"
                :name="`exclude-stage-${stage.key}`"
                :checked="draft.excludeStages.includes(stage.key)"
                @change="setStage('excludeStages', stage.key, checked($event))"
              />
              <span>{{ EventsLabels.stage(stage.key, stageOptions, locale) }}</span>
            </label>
          </div>
          <p v-if="defaultStageLabel" class="text-xs text-muted-foreground" data-test="digest-stages-hint">
            {{ eventsText('digestStagesHint', locale, { stage: defaultStageLabel }) }}
          </p>
        </template>
      </fieldset>

      <fieldset class="grid gap-3 sm:grid-cols-2" :disabled="busy">
        <label class="flex flex-col gap-1 text-sm sm:col-span-2">
          <span class="font-medium">{{ eventsText('digestRecipients', locale) }}</span>
          <textarea
            :class="inputClass"
            name="recipients"
            rows="4"
            :value="draft.recipients"
            @input="set('recipients', valueOf($event))"
          ></textarea>
          <span class="text-xs text-muted-foreground">{{ eventsText('digestRecipientsHint', locale) }}</span>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-muted-foreground">{{ eventsText('digestLanguage', locale) }}</span>
          <select
            :class="inputClass"
            name="language"
            :value="draft.language"
            @change="set('language', valueOf($event) as DigestDraft['language'])"
          >
            <option v-for="language in DigestLanguageValues" :key="language" :value="language">
              {{ languageName[language] }}
            </option>
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-muted-foreground">{{ eventsText('digestItemCap', locale) }}</span>
          <input
            :class="inputClass"
            type="number"
            min="1"
            max="50"
            name="itemCap"
            :value="draft.itemCap"
            @input="set('itemCap', valueOf($event))"
          />
        </label>
        <label v-if="!linkTemplate" class="flex flex-col gap-1 text-sm sm:col-span-2">
          <span class="text-muted-foreground">{{ eventsText('digestLinkTemplate', locale) }}</span>
          <input
            :class="inputClass"
            name="linkTemplate"
            placeholder="https://app.example.com/events/{id}"
            :value="draft.linkTemplate"
            @input="set('linkTemplate', valueOf($event))"
          />
        </label>
      </fieldset>

      <p v-if="problemText" class="text-sm text-destructive" role="alert" data-test="digest-problem">
        {{ problemText }}
      </p>
      <FailureNotice v-if="failure" :failure="failure" :locale="locale" context="admin" />
      <p v-if="savedNotice" class="text-sm text-success" role="status">{{ eventsText('saved', locale) }}</p>

      <div class="flex flex-wrap gap-2">
        <Button type="submit" :is-loading="save.pending.value" data-test="digest-save">
          {{
            eventsText('digestSave', locale)
          }}
        </Button>
        <template v-if="digest">
          <Button
            type="button"
            variant="outline"
            :is-loading="preview.pending.value"
            data-test="digest-preview"
            @click="showPreview"
          >
            {{ eventsText('digestPreview', locale) }}
          </Button>
          <Button
            type="button"
            variant="outline"
            :is-loading="send.pending.value"
            data-test="digest-send"
            @click="sendNow"
          >
            {{ eventsText('digestSendNow', locale) }}
          </Button>
          <Button
            type="button"
            variant="ghost"
            tone="danger"
            :is-loading="remove.pending.value"
            data-test="digest-delete"
            @click="deleteDigest"
          >
            {{ confirmingDelete ? eventsText('digestConfirmDelete', locale) : eventsText('digestDelete', locale) }}
          </Button>
        </template>
      </div>
    </form>

    <AlertModal :open="sendRequest !== null" @update:open="(open: boolean) => !open && cancelSend()">
      <AlertModalContent data-test="digest-send-confirm">
        <ModalHeader>
          <ModalTitle>{{ eventsText('digestSendNow', locale) }}</ModalTitle>
          <ModalDescription>{{ eventsText('digestConfirmSend', locale, { count: sendCount }) }}</ModalDescription>
        </ModalHeader>
        <ModalFooter>
          <AlertModalCancel data-test="digest-send-cancel">{{ eventsText('cancel', locale) }}</AlertModalCancel>
          <AlertModalAction data-test="digest-send-accept" @action="acceptSend">
            {{ eventsText('digestSendNow', locale) }}
          </AlertModalAction>
        </ModalFooter>
      </AlertModalContent>
    </AlertModal>

    <section
      v-if="shownPreview"
      class="space-y-3"
      aria-labelledby="digest-preview-title"
      data-test="digest-preview-result"
    >
      <h3 id="digest-preview-title" class="text-sm font-medium">
        {{ eventsText('digestPreview', locale) }} ·
        {{
          eventsText('digestMatched', locale, { selected: shownPreview.items.length, matched: shownPreview.matched })
        }}
      </h3>
      <p v-if="shownPreview.items.length === 0" class="text-sm text-muted-foreground">
        {{ eventsText('digestPreviewEmpty', locale) }}
      </p>
      <div v-for="text in shownPreview.texts" v-else :key="text.language" class="space-y-1">
        <p class="text-xs text-muted-foreground">
          <Badge size="sm" :variant="BadgeVariant.Neutral">{{ languageName[text.language] ?? text.language }}</Badge>
          · {{ text.recipients }}
        </p>
        <TelegramText :text="text.text" />
      </div>
      <ul v-if="shownPreview.notes?.length" class="list-disc pl-5 text-sm text-muted-foreground">
        <li v-for="note in shownPreview.notes" :key="note">{{ note }}</li>
      </ul>
    </section>

    <section v-if="lastRun" class="space-y-2" data-test="digest-sent">
      <h3 class="text-sm font-medium">
        {{ eventsText('digestSendNow', locale) }} ·
        {{ eventsText('digestMatched', locale, { selected: lastRun.selected, matched: lastRun.matched }) }}
      </h3>
      <DigestRecipients :recipients="lastRun.recipients ?? []" :locale="locale" />
    </section>
  </section>
</template>
