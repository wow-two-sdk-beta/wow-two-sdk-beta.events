<script setup lang="ts">
import { Button } from '@wow-two-beta/fe/presentation/actions';
import { Badge, BadgeVariant, CardLink, MediaCard } from '@wow-two-beta/fe/presentation/display';
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger, MenuItem } from '@wow-two-beta/fe/presentation/nav';
import { computed, shallowRef } from 'vue';
import type { CatalogKindModel, EventModel, TrackingModel, WorkflowStageModel } from '../../contracts';
import {
  deadlineDaysLeft,
  deadlineTone,
  eventMonthName,
  eventWhenRange,
  formatEventDates,
  formatEventWhen,
  isEventPast,
  localIsoDate,
  nextDeadline,
  readEventWhen,
} from '../dates/EventDates';
import { eventTitle, safeHttpUrl } from '../facts/EventFactsText';
import { kindGlyph } from '../facts/EventGlyphs';
import { listingRemoved, sourceLabel } from '../facts/EventSources';
import { EventsLabels } from '../text/EventsLabels';
import { type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import EventGlyph from './EventGlyph.vue';

/**
 * Shows one event as an image-first card, the look of the SCFD Posts tab. The curated image (`facts.images[0]`) fills
 * the card; with none, or a failed one, a date tile shows the start's day and month on the brand gradient with the
 * kind's icon (an undated event shows the icon alone). Lower-case glass pills at the top start give the kind, the
 * stage when the event is tracked, cancelled, postponed or online when the facts say so, and removed. At the top end, a
 * menu holds the quick stages for an event at the default stage, and a mark shows a high priority. The text panel holds
 * the title (two lines), one line with the dates and the city, then the next open deadline in its tone, or else the
 * source names. The whole card opens the event; it is dimmed when cancelled, removed or past. Organizer, scope, cost,
 * the review state and the title's language tag stay on the event's page. Sourced media never reaches it — only
 * `facts.images`.
 */
defineOptions({ name: 'EventMediaCard' });

const props = withDefaults(
  defineProps<{
    event: EventModel;
    locale?: EventsLocale;
    /** The person's calendar day, `YYYY-MM-DD`; the browser's by default. */
    today?: string;
    /** The catalog's kinds, to label the event's kind. */
    kinds?: readonly CatalogKindModel[];
    /** The event's own page; the title emits `open` on a plain click either way. */
    href?: string;
    /** The organization's tracking of the event. */
    tracking?: TrackingModel | null;
    /** The organization's workflow stages, to label the tracking's stage. */
    stages?: readonly WorkflowStageModel[];
    /** The workflow's default stage: an event at it, tracked or not, is still to triage and offers the quick stages. */
    defaultStage?: string;
    /** Stages offered in the top-end menu on an event at the default stage (Ocharo: shortlisted, skipped). */
    quickStages?: readonly string[];
    /** Whether the person may write tracking; the menu shows only then. */
    canTrack?: boolean;
    /** Whether a quick stage's write is running. */
    trackPending?: boolean;
    /** The viewer's IANA zone, to name the start time's zone only when it differs; the browser's by default. */
    viewerZone?: string;
  }>(),
  {
    locale: 'en',
    today: undefined,
    kinds: undefined,
    href: undefined,
    tracking: null,
    stages: undefined,
    defaultStage: undefined,
    quickStages: () => [],
    canTrack: false,
    trackPending: false,
    viewerZone: undefined,
  },
);

const emit = defineEmits<{
  open: [id: string];
  /** Asks to move the event to a stage: a pick from the menu on an event at the default stage. */
  track: [eventId: string, stage: string];
}>();

const day = computed(() => props.today ?? localIsoDate());
const title = computed(() => eventTitle(props.event.facts, props.locale));
const titleId = computed(() => `event-media-card-${props.event.id}`);
const lower = (text: string) => text.toLocaleLowerCase(props.locale);
const lowerFirst = (text: string) => (text ? text.charAt(0).toLocaleLowerCase(props.locale) + text.slice(1) : text);

const failedUrl = shallowRef<string | null>(null);
const image = computed(() => {
  const curated = props.event.facts.images?.[0];
  const url = curated ? safeHttpUrl(curated.url) : null;
  if (!curated || !url || url === failedUrl.value) return null;
  const attribution = curated.attribution ?? '';
  return { url, alt: attribution ? eventsText('imageBy', props.locale, { attribution }) : title.value };
});

const kind = computed(() => EventsLabels.kind(props.event.facts.kind, props.kinds, props.locale));
const stage = computed(() => props.tracking?.stage ?? null);
const stageLabel = computed(() => (stage.value ? EventsLabels.stage(stage.value, props.stages, props.locale) : ''));
const status = computed(() => props.event.facts.status ?? 'scheduled');
const cancelled = computed(() => status.value === 'cancelled');
const statusPill = computed(() =>
  status.value === 'cancelled' || status.value === 'postponed' ? EventsLabels.status(status.value, props.locale) : '',
);
const online = computed(() => props.event.facts.attendance === 'online' || status.value === 'movedOnline');
const removed = computed(() => listingRemoved(props.event));
const hasPills = computed(() =>
  Boolean(kind.value || stageLabel.value || statusPill.value || online.value || removed.value),
);
const finished = computed(() => isEventPast(props.event, day.value));
const dimmed = computed(() => cancelled.value || removed.value || finished.value);

const atDefault = computed(
  () => Boolean(props.defaultStage) && (props.tracking?.stage ?? props.defaultStage) === props.defaultStage,
);
const quick = computed(() =>
  props.canTrack && atDefault.value ? props.quickStages.filter((item) => item !== props.defaultStage) : [],
);
const highPriority = computed(() => props.tracking?.priority === 'high');

const tile = computed(() => {
  const start = readEventWhen(props.event.facts.starts);
  if (!start || start.month === undefined) return null;
  return start.day === undefined
    ? { big: eventMonthName(start.month, props.locale, 'standalone'), small: String(start.year), month: '' }
    : { big: String(start.day), small: '', month: eventMonthName(start.month, props.locale) };
});

const dates = computed(() =>
  formatEventDates(props.event, props.locale, props.viewerZone ? { viewerZone: props.viewerZone } : {}),
);
const city = computed(() => props.event.facts.place?.city?.trim() ?? '');
/** Whether a start day in the past (or today) still covers today; a month or a year estimate never reads as ongoing. */
const ongoing = computed(() => {
  if (finished.value) return false;
  const start = eventWhenRange(props.event.facts.starts);
  if (!start || start.first !== start.last || start.first > day.value) return false;
  return (eventWhenRange(props.event.facts.ends) ?? start).last >= day.value;
});

/** Whether `date` (`YYYY-MM-DD`) lies 12 or more calendar months after `from`, the point where a day and month alone mislead. */
function yearsAway(date: string, from: string): boolean {
  const months =
    (Number(date.slice(0, 4)) - Number(from.slice(0, 4))) * 12 + Number(date.slice(5, 7)) - Number(from.slice(5, 7));
  return months > 12 || (months === 12 && Number(date.slice(8, 10)) >= Number(from.slice(8, 10)));
}

const deadline = computed(() => {
  const next = nextDeadline(props.event.facts.deadlines, day.value);
  if (!next) return null;
  const parts = readEventWhen(next.at);
  const range = eventWhenRange(next.at);
  const days = deadlineDaysLeft(next, day.value);
  const date =
    parts?.month !== undefined && parts.day !== undefined && range
      ? `${parts.day} ${eventMonthName(parts.month, props.locale)}${yearsAway(range.first, day.value) ? ` ${parts.year}` : ''}`
      : formatEventWhen(next.at, props.locale);
  const label =
    next.kind === 'application'
      ? lowerFirst(eventsText('applyBy', props.locale, { date }))
      : eventsText('deadlineKindBy', props.locale, {
          kind: lower(EventsLabels.deadline(next.kind, props.locale)),
          date,
        });
  return {
    label,
    left: days === null ? '' : eventsText(days === 0 ? 'todayShort' : 'daysShort', props.locale, { count: days }),
    tone: days === null ? 'upcoming' : deadlineTone(days),
  };
});
const sources = computed(() => [
  ...new Set(
    (props.event.sources ?? []).map((item) => sourceLabel(item)).filter((name): name is string => Boolean(name)),
  ),
]);

function follow(event: MouseEvent) {
  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  emit('open', props.event.id);
}
</script>

<template>
  <MediaCard :is-dimmed="dimmed" data-test="event-media-card" :data-event-id="event.id">
    <template v-if="image" #media>
      <img
        :src="image.url"
        :alt="image.alt"
        class="size-full object-cover"
        loading="lazy"
        referrerpolicy="no-referrer"
        data-test="event-image"
        @error="failedUrl = image.url"
      />
    </template>

    <template #fallback>
      <div
        class="grid size-full place-items-center bg-gradient-to-br from-primary/30 via-primary-soft to-muted text-primary-soft-foreground"
        data-test="event-date-tile"
      >
        <div class="-mt-34 flex flex-col items-center">
          <template v-if="tile">
            <span class="text-6xl leading-none font-semibold tabular-nums" data-test="tile-day">{{ tile.big }}</span>
            <span v-if="tile.month" class="mt-1 text-2xl leading-none font-semibold" data-test="tile-month">
              {{ tile.month }}
            </span>
            <span v-else class="mt-1 text-lg leading-none font-medium" data-test="tile-year">{{ tile.small }}</span>
          </template>
          <EventGlyph
            :name="kindGlyph(event.facts.kind)"
            :size="tile ? 20 : 44"
            :class="tile ? 'mt-3 opacity-70' : 'opacity-60'"
          />
        </div>
      </div>
    </template>

    <template v-if="hasPills" #topStart>
      <div class="flex max-w-[10.5rem] flex-wrap items-center gap-1.5" data-test="event-pills">
        <Badge v-if="kind" size="sm" :variant="BadgeVariant.Glass" is-shrinkable data-test="event-kind">
          {{ lower(kind) }}
        </Badge>
        <Badge v-if="stageLabel" size="sm" :variant="BadgeVariant.Glass" is-shrinkable data-test="event-stage">
          {{ lower(stageLabel) }}
        </Badge>
        <Badge v-if="statusPill" size="sm" :variant="BadgeVariant.Glass" is-shrinkable data-test="event-status">
          {{ lower(statusPill) }}
        </Badge>
        <Badge v-if="online" size="sm" :variant="BadgeVariant.Glass" is-shrinkable data-test="event-online">
          {{ lower(EventsLabels.attendance('online', locale)) }}
        </Badge>
        <Badge v-if="removed" size="sm" :variant="BadgeVariant.Glass" is-shrinkable data-test="event-removed">
          {{ lower(eventsText('sourceStateGone', locale)) }}
        </Badge>
      </div>
    </template>

    <template v-if="highPriority || quick.length" #topEnd>
      <span
        v-if="highPriority"
        role="img"
        :aria-label="eventsText('highPriority', locale)"
        :title="eventsText('highPriority', locale)"
        class="inline-flex size-8 items-center justify-center rounded-full border border-white/20 bg-black/35 text-white backdrop-blur-md"
        data-test="event-priority"
      >
        <EventGlyph name="flag" :size="15" />
      </span>
      <DropdownMenu v-if="quick.length" placement="bottom-end">
        <DropdownMenuTrigger as-child>
          <Button
            variant="glass-surface"
            tone="neutral"
            size="sm"
            shape="circle"
            :aria-label="eventsText('stage', locale)"
            :is-disabled="trackPending"
            data-test="event-stage-menu"
          >
            <EventGlyph name="more" :size="18" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent v-bind="{ 'aria-label': eventsText('stage', locale) }">
          <MenuItem
            v-for="item in quick"
            :key="item"
            :data-stage="item"
            data-test="event-quick-stage"
            @select="emit('track', event.id, item)"
          >
            {{ EventsLabels.stage(item, stages, locale) }}
          </MenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </template>

    <h3 :id="titleId" class="line-clamp-2 break-words text-[15px] leading-snug font-semibold" data-test="event-title">
      <CardLink as-child>
        <a
          v-if="href"
          :href="href"
          class="underline-offset-4 hover:underline focus-visible:underline"
          @click="follow"
          v-text="title"
        ></a>
        <button
          v-else
          type="button"
          class="text-start underline-offset-4 hover:underline focus-visible:underline"
          @click="emit('open', event.id)"
        >
          {{ title }}
        </button>
      </CardLink>
    </h3>
    <p class="flex min-w-0 items-center gap-1.5 whitespace-nowrap text-[13px] leading-5 text-white/75">
      <span class="truncate" data-test="event-dates">{{ dates }}</span>
      <span
        v-if="finished || ongoing"
        class="shrink-0 before:me-1.5 before:content-['·']"
        :class="ongoing ? 'text-emerald-200' : 'opacity-60'"
        :data-test="finished ? 'event-finished' : 'event-ongoing'"
      >
        {{ eventsText(finished ? 'finished' : 'ongoing', locale) }}
      </span>
      <span v-if="city" class="min-w-0 truncate before:me-1.5 before:content-['·']" data-test="event-city">
        {{ city }}
      </span>
    </p>
    <p
      v-if="deadline"
      class="flex min-w-0 items-center gap-1.5 whitespace-nowrap text-[13px] leading-5"
      :class="deadline.tone === 'urgent' ? 'text-red-300' : 'text-white/75'"
      :data-tone="deadline.tone"
      data-test="event-deadline"
    >
      <span class="truncate">{{ deadline.label }}</span>
      <span v-if="deadline.left" class="shrink-0 before:me-1.5 before:content-['·']">{{ deadline.left }}</span>
    </p>
    <p v-else-if="sources.length" class="truncate text-[13px] leading-5 text-white/60" data-test="event-sources">
      {{ sources.map(lower).join(' · ') }}
    </p>
  </MediaCard>
</template>
