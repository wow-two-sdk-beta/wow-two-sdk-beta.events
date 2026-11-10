<script setup lang="ts">
import { Badge, BadgeVariant } from '@wow-two-beta/fe/presentation/display';
import { SkeletonState } from '@wow-two-beta/fe/presentation/feedback';
import { computed, onMounted, watch } from 'vue';
import type { CatalogKindModel } from '../../contracts';
import type { EventsClient } from '../client/CreateEventsClient';
import {
  daysLeftLabel,
  deadlineDaysLeft,
  deadlineTone,
  formatEventDates,
  formatEventWhen,
  formatTimestamp,
  localIsoDate,
} from '../dates/EventDates';
import { eventTitle, formatEventPlace, formatOfferPrice, safeHttpUrl } from '../facts/EventFactsText';
import { useEventsRequest } from '../hooks/UseEventsRequest';
import { EventsLabels } from '../text/EventsLabels';
import { pickText, type EventsLocale } from '../text/EventsLocale';
import { eventsText } from '../text/EventsMessages';
import FailureNotice from './FailureNotice.vue';
import TrackingForm from './TrackingForm.vue';

/**
 * Shows one event with every reviewed fact: curated images, dates as precisely as known, place, deadlines, offers,
 * links and sources — and, when `trackable`, the organization's tracking form. Sourced media never reaches it.
 */
defineOptions({ name: 'EventDetail' });

const props = withDefaults(
  defineProps<{
    client: EventsClient;
    catalog: string;
    eventId: string;
    locale?: EventsLocale;
    /** The person's calendar day, `YYYY-MM-DD`; the browser's by default. */
    today?: string;
    kinds?: readonly CatalogKindModel[];
    /** Whether to show the organization's tracking beside the event. */
    trackable?: boolean;
    /** Whether the tracking form may write (`events:track`). */
    canTrack?: boolean;
  }>(),
  { locale: 'en', today: undefined, kinds: undefined, trackable: false, canTrack: true },
);

const request = useEventsRequest((signal) => props.client.getEvent(props.catalog, props.eventId, signal));
const event = computed(() => request.value.value);
const facts = computed(() => event.value?.facts);
const day = computed(() => props.today ?? localIsoDate());
const images = computed(() =>
  (facts.value?.images ?? [])
    .map((image) => ({ ...image, safe: safeHttpUrl(image.url) }))
    .filter((image): image is typeof image & { safe: string } => image.safe !== null),
);
const links = computed(() =>
  [
    { key: 'website' as const, url: safeHttpUrl(facts.value?.website) },
    { key: 'tickets' as const, url: safeHttpUrl(facts.value?.ticketUrl) },
    { key: 'joinOnline' as const, url: safeHttpUrl(facts.value?.onlineUrl) },
  ].filter((link): link is { key: 'website' | 'tickets' | 'joinOnline'; url: string } => link.url !== null),
);
const summary = computed(() => pickText(facts.value?.summary, props.locale));
const place = computed(() => (facts.value ? formatEventPlace(facts.value, props.locale) : ''));
const address = computed(() => facts.value?.place?.address ?? '');

onMounted(() => request.run());
watch([() => props.catalog, () => props.eventId, () => props.client], () => request.run());
</script>

<template>
  <div class="space-y-4">
    <div v-if="request.pending.value && !event" class="space-y-3" aria-busy="true">
      <span role="status" class="sr-only">{{ eventsText('loadingEvent', locale) }}</span>
      <div class="space-y-3" aria-hidden="true">
        <SkeletonState class="h-8 w-3/5" />
        <SkeletonState class="h-4 w-2/5" />
        <SkeletonState class="h-32 w-full" />
      </div>
    </div>
    <template v-else-if="request.failure.value">
      <h2 class="text-xl font-semibold">{{ eventsText('eventUnavailable', locale) }}</h2>
      <FailureNotice :failure="request.failure.value" :locale="locale" retry @retry="request.run()" />
    </template>
    <article v-else-if="event && facts" class="space-y-5" aria-labelledby="event-detail-title">
      <header class="space-y-2">
        <div class="flex flex-wrap gap-2">
          <Badge v-if="event.review !== 'published'" size="sm" :variant="BadgeVariant.Warning">
            {{ EventsLabels.review(event.review, locale) }}
          </Badge>
          <Badge v-if="facts.kind" size="sm" :variant="BadgeVariant.Outline">
            {{ EventsLabels.kind(facts.kind, kinds, locale) }}
          </Badge>
          <Badge
            v-if="facts.status && facts.status !== 'scheduled'"
            size="sm"
            :variant="facts.status === 'cancelled' ? BadgeVariant.Danger : BadgeVariant.Info"
          >
            {{ EventsLabels.status(facts.status, locale) }}
          </Badge>
        </div>
        <h2 id="event-detail-title" class="break-words text-2xl font-semibold">{{ eventTitle(facts, locale) }}</h2>
        <p v-if="facts.organizer?.name" class="text-sm text-muted-foreground">
          <a
            v-if="safeHttpUrl(facts.organizer.website)"
            :href="safeHttpUrl(facts.organizer.website) ?? undefined"
            rel="noopener noreferrer"
            target="_blank"
            class="underline"
          >
            {{ facts.organizer.name }}
          </a>
          <template v-else>{{ facts.organizer.name }}</template>
        </p>
      </header>

      <div v-if="images.length" class="flex gap-3 overflow-x-auto" data-test="event-images">
        <figure v-for="image in images" :key="image.safe" class="shrink-0">
          <img
            :src="image.safe"
            :alt="image.attribution ? eventsText('imageBy', locale, { attribution: image.attribution }) : ''"
            class="h-48 rounded-md object-cover"
            loading="lazy"
          />
          <figcaption v-if="image.attribution" class="mt-1 text-xs text-muted-foreground">
            {{ image.attribution }}
          </figcaption>
        </figure>
      </div>

      <p v-if="summary" class="whitespace-pre-line">{{ summary }}</p>

      <dl class="grid gap-4 sm:grid-cols-2">
        <div>
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ eventsText('when', locale) }}</dt>
          <dd class="font-medium" data-test="event-dates">{{ formatEventDates(event, locale) }}</dd>
          <dd v-if="facts.timeZone" class="text-xs text-muted-foreground">{{ facts.timeZone }}</dd>
        </div>
        <div v-if="place || address || facts.attendance">
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ eventsText('where', locale) }}</dt>
          <dd v-if="place">{{ place }}</dd>
          <dd v-if="address" class="text-sm text-muted-foreground">{{ address }}</dd>
          <dd v-if="facts.attendance" class="text-sm text-muted-foreground">
            {{ EventsLabels.attendance(facts.attendance, locale) }}
            <template v-if="facts.audience"> · {{ EventsLabels.audience(facts.audience, locale) }}</template>
          </dd>
        </div>
        <div v-if="facts.languages?.length">
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ eventsText('languages', locale) }}</dt>
          <dd>{{ facts.languages.join(', ') }}</dd>
        </div>
        <div v-if="facts.tags?.length">
          <dt class="text-xs font-medium uppercase text-muted-foreground">{{ eventsText('tags', locale) }}</dt>
          <dd class="flex flex-wrap gap-1">
            <Badge v-for="tag in facts.tags" :key="tag" size="sm" :variant="BadgeVariant.Neutral">{{ tag }}</Badge>
          </dd>
        </div>
      </dl>

      <section v-if="facts.deadlines?.length" class="space-y-2" aria-labelledby="event-deadlines-title">
        <h3 id="event-deadlines-title" class="font-semibold">{{ eventsText('deadlines', locale) }}</h3>
        <ul class="space-y-1 text-sm">
          <li
            v-for="(deadline, index) in facts.deadlines"
            :key="index"
            class="flex flex-wrap items-center gap-x-2"
            data-test="event-deadline"
          >
            <span class="font-medium">{{ deadline.label || EventsLabels.deadline(deadline.kind, locale) }}:</span>
            <span>{{ formatEventWhen(deadline.at, locale) }}</span>
            <span
              v-if="deadlineDaysLeft(deadline, day) !== null"
              :data-tone="deadlineTone(deadlineDaysLeft(deadline, day) ?? 0)"
              class="text-muted-foreground"
            >
              · {{ daysLeftLabel(deadlineDaysLeft(deadline, day) ?? 0, locale) }}
            </span>
            <a
              v-if="safeHttpUrl(deadline.url)"
              :href="safeHttpUrl(deadline.url) ?? undefined"
              target="_blank"
              rel="noopener noreferrer"
              class="underline"
            >
              {{ eventsText('links', locale) }}
            </a>
          </li>
        </ul>
      </section>

      <section v-if="facts.offers?.length" class="space-y-2" aria-labelledby="event-offers-title">
        <h3 id="event-offers-title" class="font-semibold">{{ eventsText('offers', locale) }}</h3>
        <ul class="space-y-1 text-sm">
          <li v-for="(offer, index) in facts.offers" :key="index" class="flex flex-wrap gap-x-2">
            <span class="font-medium">{{ offer.name || EventsLabels.offer(offer.kind, locale) }}:</span>
            <span>{{ formatOfferPrice(offer, locale) }}</span>
            <span v-if="offer.terms" class="text-muted-foreground">· {{ offer.terms }}</span>
            <a
              v-if="safeHttpUrl(offer.url)"
              :href="safeHttpUrl(offer.url) ?? undefined"
              target="_blank"
              rel="noopener noreferrer"
              class="underline"
            >
              {{ eventsText('links', locale) }}
            </a>
          </li>
        </ul>
      </section>

      <section v-if="links.length" class="space-y-2" aria-labelledby="event-links-title">
        <h3 id="event-links-title" class="font-semibold">{{ eventsText('links', locale) }}</h3>
        <ul class="flex flex-wrap gap-3 text-sm">
          <li v-for="link in links" :key="link.key">
            <a :href="link.url" target="_blank" rel="noopener noreferrer" class="underline">{{
              eventsText(link.key, locale)
            }}</a>
          </li>
        </ul>
      </section>

      <section v-if="event.sources?.length" class="space-y-2" aria-labelledby="event-sources-title">
        <h3 id="event-sources-title" class="font-semibold">{{ eventsText('sources', locale) }}</h3>
        <ul class="space-y-1 text-xs text-muted-foreground">
          <li v-for="source in event.sources" :key="`${source.kind}-${source.id}`">
            {{ source.kind }} ·
            <a
              v-if="safeHttpUrl(source.url)"
              :href="safeHttpUrl(source.url) ?? undefined"
              target="_blank"
              rel="noopener noreferrer"
              class="underline"
            >
              {{ source.url }}
            </a>
            <template v-else>{{ source.id }}</template>
            · {{ eventsText('firstSeen', locale) }} {{ formatTimestamp(source.firstSeenAtUtc, locale) }} ·
            {{ eventsText('lastSeen', locale) }} {{ formatTimestamp(source.lastSeenAtUtc, locale) }}
          </li>
        </ul>
      </section>
    </article>

    <TrackingForm
      v-if="trackable && event"
      :client="client"
      :catalog="catalog"
      :event-id="eventId"
      :locale="locale"
      :can-edit="canTrack"
    />
  </div>
</template>
