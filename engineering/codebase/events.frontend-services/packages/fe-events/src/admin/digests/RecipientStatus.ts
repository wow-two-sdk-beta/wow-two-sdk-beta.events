import type { DigestRecipientStatus } from '../../contracts';
import type { EventsMessageKey } from '../../kit/text/EventsMessages';

/**
 * The message that names each recipient status in plain words (`not_linked` reads "No Telegram link"): the run history's
 * counts and a run's recipient list say it the same way, and never show Identity's raw word.
 */
export const recipientStatusKeys: Readonly<Record<DigestRecipientStatus, EventsMessageKey>> = {
  pending: 'recipientPending',
  sent: 'recipientSent',
  not_linked: 'recipientNotLinked',
  muted: 'recipientMuted',
  failed: 'recipientFailed',
};

/** The statuses the run history counts, in the order it writes them. */
export const countedRecipientStatuses = [
  'sent',
  'not_linked',
  'muted',
  'failed',
] as const satisfies readonly DigestRecipientStatus[];
