import {
  DigestChannelConstants,
  DigestLanguageValues,
  type DigestDay,
  type DigestLanguage,
  type DigestModel,
  type DigestRecipient,
  type DigestSpec,
  type EventStatus,
} from '../../contracts';
import type { EventsLocale } from '../../kit/text/EventsLocale';

/** Holds a digest editor's fields as the form edits them: lists as text, numbers as text. */
export interface DigestDraft {
  name: string;
  enabled: boolean;
  channel: string;
  days: DigestDay[];
  times: string;
  timeZone: string;
  newSinceLastRun: boolean;
  startingWithinDays: string;
  deadlineWithinDays: string;
  statuses: EventStatus[];
  kinds: string;
  cities: string;
  trackedOnly: boolean;
  recipients: string;
  language: DigestLanguage;
  itemCap: string;
  linkTemplate: string;
}

/** Names what a draft got wrong, as a message key and its parameters. */
export type DigestDraftProblem =
  | { readonly key: 'digestDaysRequired' }
  | { readonly key: 'digestTimesInvalid' }
  | { readonly key: 'digestRecipientsInvalid'; readonly line: number };

/**
 * Holds the default a product starts a new digest from: Ocharo's weekly Monday 09:00 in Tashkent with new events, those
 * starting within 30 days and deadlines within 14 days, in Uzbek. A product passes its own as the editor's `defaults`.
 */
export const defaultDigestSpec: DigestSpec = {
  name: 'Weekly events',
  enabled: true,
  channel: DigestChannelConstants.telegram,
  schedule: { days: ['monday'], times: ['09:00'], timeZone: 'Asia/Tashkent' },
  selection: { newSinceLastRun: true, startingWithinDays: 30, deadlineWithinDays: 14 },
  recipients: [],
  language: 'uz-Latn',
  itemCap: 20,
};

/** Reads a digest, or a new one's defaults, into the editor's fields. */
export function digestDraft(source: DigestModel | DigestSpec): DigestDraft {
  const selection = source.selection ?? {};
  return {
    name: source.name,
    enabled: source.enabled ?? true,
    channel: source.channel ?? DigestChannelConstants.telegram,
    days: [...source.schedule.days],
    times: source.schedule.times.join(', '),
    timeZone: source.schedule.timeZone,
    newSinceLastRun: selection.newSinceLastRun ?? false,
    startingWithinDays: selection.startingWithinDays?.toString() ?? '',
    deadlineWithinDays: selection.deadlineWithinDays?.toString() ?? '',
    statuses: [...(selection.statuses ?? [])],
    kinds: (selection.kinds ?? []).join(', '),
    cities: (selection.cities ?? []).join(', '),
    trackedOnly: selection.trackedOnly ?? false,
    recipients: formatRecipients(source.recipients),
    language: source.language ?? 'uz-Latn',
    itemCap: (source.itemCap ?? 20).toString(),
    linkTemplate: source.linkTemplate ?? '',
  };
}

/** Writes the editor's fields as a spec; the service validates the rest and names the field it refuses. */
export function digestSpec(
  draft: DigestDraft,
  expectedRevision: number | null,
): { spec: DigestSpec } | { problem: DigestDraftProblem } {
  if (draft.days.length === 0) return { problem: { key: 'digestDaysRequired' } };
  const times = list(draft.times);
  if (times.length === 0 || times.some((time) => !/^([01]\d|2[0-3]):[0-5]\d$/.test(time)))
    return { problem: { key: 'digestTimesInvalid' } };
  const recipients = parseRecipients(draft.recipients);
  if ('line' in recipients) return { problem: { key: 'digestRecipientsInvalid', line: recipients.line } };

  return {
    spec: {
      name: draft.name.trim(),
      enabled: draft.enabled,
      channel: draft.channel,
      schedule: { days: [...draft.days], times, timeZone: draft.timeZone.trim() },
      selection: {
        newSinceLastRun: draft.newSinceLastRun,
        startingWithinDays: wholeNumber(draft.startingWithinDays),
        deadlineWithinDays: wholeNumber(draft.deadlineWithinDays),
        statuses: [...draft.statuses],
        kinds: list(draft.kinds),
        cities: list(draft.cities),
        trackedOnly: draft.trackedOnly,
      },
      recipients: recipients.list,
      language: draft.language,
      itemCap: wholeNumber(draft.itemCap) ?? 20,
      linkTemplate: draft.linkTemplate.trim() || null,
      expectedRevision,
    },
  };
}

/** Reads recipient lines: `sub` or `role:name`, each with an optional language after a space. */
export function parseRecipients(text: string): { list: DigestRecipient[] } | { line: number } {
  const recipients: DigestRecipient[] = [];
  const lines = text.split(/\r?\n/);
  for (let index = 0; index < lines.length; index++) {
    const parts = lines[index]!.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) continue;
    const [who, language, ...rest] = parts as [string, string | undefined, ...string[]];
    if (rest.length > 0 || (language !== undefined && !isLanguage(language))) return { line: index + 1 };
    const role = who.startsWith('role:') ? who.slice('role:'.length) : null;
    if (role === '') return { line: index + 1 };
    recipients.push({
      ...(role !== null ? { role } : { subject: who }),
      ...(language !== undefined ? { language: language as DigestLanguage } : {}),
    });
  }
  return { list: recipients };
}

/** Writes recipients as the editor's lines. */
export function formatRecipients(recipients: readonly DigestRecipient[]): string {
  return recipients
    .map((recipient) =>
      [recipient.role ? `role:${recipient.role}` : (recipient.subject ?? ''), recipient.language ?? '']
        .filter(Boolean)
        .join(' '),
    )
    .join('\n');
}

/** Writes an instant in a zone, such as a digest's next run in Tashkent time; the zone's name follows. */
export function formatInZone(utc: string, timeZone: string, locale: EventsLocale): string {
  const tag = locale === 'uz' ? 'uz-Latn' : locale;
  try {
    const text = new Intl.DateTimeFormat(tag, {
      timeZone,
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    }).format(new Date(utc));
    return `${text} (${timeZone})`;
  } catch {
    return utc;
  }
}

/** Maps the kit's locale to the digest language a new digest starts in. */
export function digestLanguageOf(locale: EventsLocale): DigestLanguage {
  return locale === 'uz' ? 'uz-Latn' : locale;
}

const isLanguage = (value: string): value is DigestLanguage =>
  (DigestLanguageValues as readonly string[]).includes(value);
const list = (text: string) =>
  text
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
function wholeNumber(text: string): number | null {
  const value = text.trim();
  return /^\d+$/.test(value) ? Number(value) : null;
}
