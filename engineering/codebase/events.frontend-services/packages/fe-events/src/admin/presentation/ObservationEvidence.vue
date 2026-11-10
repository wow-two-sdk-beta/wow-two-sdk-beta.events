<script setup lang="ts">
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { computed } from 'vue';
import type { JsonValue, ObservationModel } from '../../contracts';
import { formatEventSpan, formatTimestamp } from '../../kit/dates/EventDates';
import { eventTitle, formatEventPlace, safeHttpUrl } from '../../kit/facts/EventFactsText';
import { EventsLabels } from '../../kit/text/EventsLabels';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText } from '../../kit/text/EventsMessages';

/**
 * Shows one observation's evidence for a reviewer: what was delivered and when, every source link with its capture, the
 * quarantine reason, and the proposed facts and raw payload as text. Everything sourced is labelled source evidence and
 * shown as text and links only — its images and text are never rendered as event content.
 */
defineOptions({ name: 'ObservationEvidence' });

const props = withDefaults(
  defineProps<{
    observation: ObservationModel;
    locale?: EventsLocale;
    /** Whether the proposed facts are summarized (off when the caller shows them as a change). */
    showProposed?: boolean;
  }>(),
  { locale: 'en', showProposed: true },
);

const stateVariant = computed(() => {
  switch (props.observation.state) {
    case 'quarantined':
      return BadgeVariant.Danger;
    case 'pending':
      return BadgeVariant.Warning;
    case 'applied':
    case 'accepted':
      return BadgeVariant.Success;
    default:
      return BadgeVariant.Neutral;
  }
});
const proposed = computed(() => props.observation.proposed ?? null);
const proposedImages = computed(() => (proposed.value?.images ?? []).map((image) => image.url));
const json = (value: JsonValue | undefined) => JSON.stringify(value ?? null, null, 2);
const facts = computed(() =>
  [
    { key: 'delivery', value: props.observation.deliveryEventId },
    { key: 'item', value: props.observation.itemId },
    {
      key: 'post',
      value: props.observation.postId
        ? `${props.observation.postId}${props.observation.revision != null ? ` · r${props.observation.revision}` : ''}`
        : null,
    },
    { key: 'instance', value: props.observation.sourceInstance },
    { key: 'subscription', value: props.observation.subscriptionId },
    { key: 'pipeline', value: props.observation.pipelineId },
    { key: 'mappingVersion', value: props.observation.mappingVersion },
    { key: 'reason', value: props.observation.reason },
  ].filter(
    (
      item,
    ): item is {
      key: 'delivery' | 'item' | 'post' | 'instance' | 'subscription' | 'pipeline' | 'mappingVersion' | 'reason';
      value: string;
    } => Boolean(item.value),
  ),
);
</script>

<template>
  <section
    class="space-y-3 rounded-md border border-border p-3 text-sm"
    :class="{ 'border-destructive/50': observation.state === 'quarantined' }"
    :aria-label="eventsText('sourceEvidence', locale)"
    data-test="observation-evidence"
    :data-state="observation.state"
  >
    <div class="flex flex-wrap items-center gap-2">
      <Badge size="sm" :variant="stateVariant">{{ EventsLabels.observation(observation.state, locale) }}</Badge>
      <code class="text-xs">{{ observation.type }}</code>
      <span class="text-xs text-muted-foreground">{{ observation.sourceKind }}</span>
      <span class="text-xs text-muted-foreground">
        {{ eventsText('observed', locale) }}
        <time :datetime="observation.observedAtUtc">{{ formatTimestamp(observation.observedAtUtc, locale) }}</time>
        · {{ eventsText('received', locale) }}
        <time :datetime="observation.receivedAtUtc">{{ formatTimestamp(observation.receivedAtUtc, locale) }}</time>
      </span>
    </div>

    <p
      v-if="observation.quarantineReason"
      class="rounded-md bg-destructive-soft px-2 py-1 text-destructive-soft-foreground"
      data-test="quarantine-reason"
    >
      {{ eventsText('quarantineReason', locale) }}: {{ observation.quarantineReason }}
    </p>

    <dl v-if="facts.length" class="grid gap-x-4 gap-y-1 text-xs sm:grid-cols-2">
      <div v-for="item in facts" :key="item.key" class="flex min-w-0 gap-1">
        <dt class="text-muted-foreground">{{ eventsText(item.key, locale) }}:</dt>
        <dd class="min-w-0 break-all font-mono">{{ item.value }}</dd>
      </div>
    </dl>

    <ul v-if="observation.links?.length" class="space-y-1 text-xs" data-test="observation-links">
      <li v-for="(link, index) in observation.links" :key="index" class="break-all">
        <a
          v-if="safeHttpUrl(link.url)"
          :href="safeHttpUrl(link.url) ?? undefined"
          target="_blank"
          rel="noopener noreferrer nofollow"
          class="underline"
        >
          {{ link.url }}
        </a>
        <span v-else-if="link.url">{{ link.url }}</span>
        <span v-if="link.sourceId" class="text-muted-foreground">
          · {{ eventsText('sourceId', locale) }} {{ link.sourceId }}</span>
        <span v-if="link.captureId" class="text-muted-foreground">
          · {{ eventsText('capture', locale) }} {{ link.captureId }}</span>
        <span v-if="link.capturedAtUtc" class="text-muted-foreground">
          · {{ eventsText('captured', locale) }} {{ formatTimestamp(link.capturedAtUtc, locale) }}
        </span>
        <details v-if="link.sourceFields != null" class="mt-1">
          <summary class="cursor-pointer text-muted-foreground">
            {{ eventsText('sourceFields', locale) }} ({{ eventsText('sourceEvidence', locale) }})
          </summary>
          <pre class="mt-1 max-h-48 overflow-auto rounded bg-muted p-2 text-xs">{{ json(link.sourceFields) }}</pre>
        </details>
      </li>
    </ul>

    <div v-if="showProposed && proposed" class="space-y-1 text-xs" data-test="observation-proposed">
      <p class="font-medium text-muted-foreground">
        {{ eventsText('proposedValue', locale) }} — {{ eventsText('sourceEvidence', locale) }}
      </p>
      <p class="text-sm">{{ eventTitle(proposed, locale) }}</p>
      <p>{{ formatEventSpan(proposed.starts, proposed.ends, locale) }}</p>
      <p v-if="formatEventPlace(proposed, locale)">{{ formatEventPlace(proposed, locale) }}</p>
      <ul v-if="proposedImages.length" class="break-all text-muted-foreground">
        <li v-for="url in proposedImages" :key="url">{{ url }}</li>
      </ul>
    </div>

    <details v-if="observation.payload != null">
      <summary class="cursor-pointer text-xs text-muted-foreground">
        {{ eventsText('payload', locale) }} ({{ eventsText('sourceEvidence', locale) }})
      </summary>
      <p class="mt-1 text-xs text-muted-foreground">{{ eventsText('sourceEvidenceNote', locale) }}</p>
      <pre class="mt-1 max-h-64 overflow-auto rounded bg-muted p-2 text-xs" data-test="observation-payload">{{
        json(observation.payload)
      }}</pre>
    </details>
  </section>
</template>
