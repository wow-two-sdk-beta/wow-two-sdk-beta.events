<script setup lang="ts">
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import type { DigestRecipientStatus, DigestRunRecipientModel } from '../../contracts';
import type { EventsLocale } from '../../kit/text/EventsLocale';
import { eventsText, type EventsMessageKey } from '../../kit/text/EventsMessages';

/** Lists a run's recipients with what became of each message, in Identity's words (`sent`, `not_linked`, …). */
defineOptions({ name: 'DigestRecipients' });

withDefaults(defineProps<{ recipients: readonly DigestRunRecipientModel[]; locale?: EventsLocale }>(), {
  locale: 'en',
});

const labels: Readonly<Record<DigestRecipientStatus, EventsMessageKey>> = {
  pending: 'recipientPending',
  sent: 'recipientSent',
  not_linked: 'recipientNotLinked',
  muted: 'recipientMuted',
  failed: 'recipientFailed',
};
const variants: Readonly<Record<DigestRecipientStatus, BadgeVariant>> = {
  pending: BadgeVariant.Info,
  sent: BadgeVariant.Success,
  not_linked: BadgeVariant.Warning,
  muted: BadgeVariant.Neutral,
  failed: BadgeVariant.Danger,
};
</script>

<template>
  <ul class="space-y-1 text-sm" data-test="digest-recipients">
    <li
      v-for="recipient in recipients"
      :key="recipient.subject"
      class="flex flex-wrap items-center gap-2"
      data-test="digest-recipient"
      :data-status="recipient.status"
    >
      <span class="font-mono text-xs">{{ recipient.subject }}</span>
      <Badge :variant="variants[recipient.status] ?? BadgeVariant.Neutral">
        {{ labels[recipient.status] ? eventsText(labels[recipient.status], locale) : recipient.status }}
      </Badge>
      <code class="text-xs text-muted-foreground">{{ recipient.status }}</code>
      <span v-if="recipient.error" class="text-xs text-destructive">{{ recipient.error }}</span>
    </li>
  </ul>
</template>
