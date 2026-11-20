# @wow-two-beta/fe-events

The Events domain's UI: a shared catalog of dated happenings (fairs, fashion weeks, competitions) curated in review,
with each organization's private tracking. Sourced records serve internal use only, and every item credits its source by the external link; a sourced
event's images arrive in `facts.images` with the source URL, and `fileRef` when SCFD holds a copy.

```bash
pnpm add @wow-two-beta/fe-events @wow-two-beta/fe vue
```

```ts
import { EventsRoutes, type EventModel } from '@wow-two-beta/fe-events/contracts';
import { createEventsClient, EventsFeed, EventDetail, formatEventWhen } from '@wow-two-beta/fe-events/kit';
import { ReviewQueue, ObservationsList, IntakeStatusPanel } from '@wow-two-beta/fe-events/admin';

// A product proxies the Events routes through its backend; the console calls them same-origin.
const client = createEventsClient({ baseUrl: '/api/desk/events' });
```

Every component takes the `client` (or callbacks) as a prop and keeps no global store; texts come in `en`, `ru` and
`uz` through the `locale` prop. The components are styled with Tailwind utilities on the fe sdk theme; add the package
to the consumer's Tailwind sources:

```css
@source '../../node_modules/@wow-two-beta/fe-events/dist';
```

| Export | Holds |
|---|---|
| `./contracts` | The TypeScript mirror of the be contract (camelCase JSON, enums as camelCase strings, localized text as `Record<locale, string>`), its constants and `EventsRoutes`, the route builders, with the query mappers (`trackingQueryParams`, `trackingFacetsQueryParams`, …) |
| `./kit` | `createEventsClient` (every route, `{ data }` unwrapped, failures mapped by status), date helpers honest about precision and certainty (`formatEventWhen`, `formatEventSpan`, `groupEventsByMonth`, `sortEventsByDate`), `downloadEventsCalendar` and one event's `createEventCalendar`, the title, price, region and source helpers (`pickTextEntry`, `eventTitleLanguage`, `formatOfferPrice`, `regionQuery`, `listingRemoved`, `eventDateRange`), and `EventsFeed`, `EventMediaCard`, `EventsDeadlines`, `EventDetail`, `EventProposalForm`, `TrackingForm` |
| `./admin` | The reviewer's and admin's screens: `ReviewQueue`, `ObservationsList`, `ObservationEvidence`, `IntakeStatusPanel`, `CatalogEventsTable`, `EventSourcesPanel` (an event's sources with the split of a joined one), `ImportPanel`, and scheduled digests: `DigestsPanel` (list, editor, run history), `DigestEditor`, `DigestRunHistory`, `DigestRecipients`, `TelegramText` |

- Dates never claim more than they know: a month stays a month (`Oct 2026`), an estimate reads `≈ Oct 2026 (expected)`
  even when it names a day or a time, an unknown date `Dates not announced`, and undated events sort last. A start time
  names its zone (`19:00 GMT+5`) only when the event's zone runs at another offset than the viewer's.
- Titles match by language prefix (`ru` finds `ru-Cyrl`, `uz` finds `uz-Latn`); a card and the detail tag a title written
  in another language (`RU`).
- An organization's page lists its tracking with `<EventsFeed tracking :hidden-stages :regions :quick-stages :can-track
  :attention>`: every event is an `EventMediaCard` (stage pill when tracked, an untracked event the default stage's
  quick actions in the card menu), `hiddenStages` are left out unless the "Show …" toggle or a stage pick asks, and
  `quickStages` move an event at the default stage in one click. Mark a stage `lost` in the workflow to keep its events
  out of the deadlines. `EventsDeadlines` takes `deadlineKinds` and `eventHref`; `EventDetail` takes `trackable`,
  `regions`, `shownFields`, `hiddenFields` and offers the event's own calendar file; `TrackingForm` keeps the draft on a
  409, emits `dirty`, and shows a schema field marked `readOnly` without offering it.
- The tracking list's query (`TrackingQuery`) takes `kinds`, `cities`, `sources`, `status`, `publishedSince` and `sort`
  beside the filters before, and the client sends a list as a repeated key (`cities=tashkent&cities=samarkand`).
  `sources` matches the name each source of an event is stored under (`afisha` finds `Afisha`, without case); the
  reserved `curated` (`EventsConstants.curatedSource`) matches the events no intake source reported, imports and
  curators' entries; values OR together. `sort` is `starts` (default, not sent), `added` (newest published first) or
  `deadline` (nearest open deadline first, none last). A `nextCursor` holds for the sort it came from: change the sort
  and start from no cursor, since another sort answers a validation failure.
- `client.getTrackingFacets(catalog, query)` (`GET …/tracking/facets`) answers `{ total, countries, cities, kinds,
  stages, priorities, sources, openDeadline }` for the list's own query: each facet is counted without its own
  selection, so a chip shows what picking it would list, and the counts come from the server because the feed scrolls
  and never holds the whole catalog. A count is `{ value, count, label? }` (`value` is what the filter takes back; only
  `sources` carries a `label`, the name as stored); `kinds` and `stages` list the catalog's kinds and the workflow's
  stages in order with zeros, `priorities` all three with zeros, an untracked event counts under the default stage, and
  `openDeadline` is `{ before, count }`: the events with an open deadline on or before `before`, the query's
  `deadlineBefore` else 30 days ahead. The query's sort, cursor and limit are not sent.
- `EventsFeed` v2 (fe sdk `0.0.56`, `FacetBar`, `useAdaptiveList`) is a grid of `EventMediaCard`s
  (`repeat(auto-fill, minmax(min(100%, 17rem), 1fr))`, fe sdk `MediaCard`) behind one toolbar:
  - the match pill "{n} of {N}" (tracking): `n` is the current query's facets `total`, `N` a base query's total (the
    default upcoming view, no chosen filter, hidden stages still left out), read on mount and after a tracking write;
  - search (debounced ~250 ms), inline scope chips from `regions` (all, each region; counts summed from the `countries`
    facet) and inline kind chips with counts (several kinds with `tracking`, one without);
  - a "Filters" popover: stages (an untracked event counts under the default stage), city, source (labels as stored,
    `curated` as "Curated"), three `FacetKind.Toggle` switch rows — "High priority" (`priority=high`, counted from the
    `high` priority), "Deadline within 30 days" (`deadlineBefore` 30 days from today, counted from `openDeadline`) and
    "Show {hidden stages}" (lifts `excludeStages`) — and the date presets (upcoming by default, this month, next 3
    months, this year, past, custom: the fe sdk's `DateRangePicker` appears at the end of the bar); each pick shows as a
    removable chip, a toggle that is on as one chip with its bare label closed by `Remove {label}`;
  - the sort (`starts`, `added`, `deadline`; tracking only), then the proposal button (`canPropose`) and an overflow menu
    holding the calendar download.
  Counts come from one `getTrackingFacets` call per query change (the list's own query, aborted when superseded; each
  facet from its own array); a facets failure leaves the chips without counts. The list scrolls: `useAdaptiveList`
  loads the next cursor ahead of the reader, with no button, and starts over from no cursor when a filter or the sort
  changes. Month dividers show under the `starts` sort only (undated events last); `added` and `deadline` keep the
  server's order in a flat grid. Without `tracking` the same toolbar filters the catalog list: search, kind, scope and
  the date presets, with no counts, no pill, no sort and no stage, city, source or priority facets. `eventDateRange(preset,
  today, custom?)` gives the `from` and `to` a preset means.
  - Attention pills sit between the toolbar and the grid and hide at a count of 0: the built-in "{n} deadlines in 14
    days" (open `application` deadlines of the tracking summary, expanding to `EventsDeadlines`) and the consumer's
    `attention` (`{ key, label, count }[]`), each expanding to the `#attention` slot (`{ key, close }`).
  - Breaks from v1: `EventCard` and the `variant` prop are gone (every event is a card), `shownFields` is dropped
    from the feed (cost stays off cards; `TrackingShownField` stays for `EventDetail`), and the peer floor is `0.0.56`.
    The card shows `facts.images[0]`, else a date tile (the start's day in large type with the month under it, the kind's icon; a
    month-only start the full month name with the year under it and no large day; an undated event the icon alone). Top start: lower-case glass pills for the kind, the stage when tracked, cancelled /
    postponed / online and removed. Top end: a menu of `quickStages` for an event at the default stage, and a mark for
    high priority. Panel: the title in two lines, "dates · city" (`ongoing` / `finished`), then the next open deadline
    ("apply by 3 Mar · 5d", "registration by 20 Jan · 71d"; the year only 12 or more months out; red when urgent), else
    the source names. The whole card opens the event (`eventHref`, else `open`), and it is dimmed when cancelled,
    removed or past. Organizer, scope, cost, the review state and the title-language tag stay on the event's page.
- Card zones are slots (`EventMediaCard`; `EventsFeed` forwards `card-{zone}` to every card). Each defaults to the
  content above, so a consumer swaps one zone and keeps the rest; every slot gets `{ event, tracking }` plus the values
  its zone renders (types `EventCard*Slot`, zones `EventCardZones`):
  | Slot (feed: `card-…`) | Zone | Replaces | Scope beside `event` and `tracking` |
  |---|---|---|---|
  | `media` | behind the card | the curated image | `image` (`{ url, alt }` or null) |
  | `fallback` | behind the card, no media | the date tile | `tile` (`{ big, small, month }`: the day, the year of a month-only start, the month; or null), `kindKey` |
  | `pills` | top start | kind, stage, status, online and removed pills | `kind`, `stage`, `status` (labels), `online`, `removed` |
  | `actions` | top end | the high-priority mark and the quick-stage menu | `highPriority`, `quickStages`, `pending`, `track(stage)` |
  | `title` | the heading | the stretched link or `open` button | `title`, `href`, `open()`, `follow(event)` |
  | `meta` | first text line | dates, ongoing or finished, city | `dates`, `city`, `finished`, `ongoing` |
  | `detail` | second text line | the next deadline, else the sources | `deadline` (`{ label, left, tone }` or null), `sources` |
  A given `media` slot always shows (the `fallback` only shows with no media); a given `pills` or `actions` slot shows
  even when the default would be empty. A zone with no slot keeps its default.
- `EventFactsFields` (and so `EventProposalForm` and the review editor) picks the country with the fe sdk's
  `CountryPicker`: a searchable list in the viewer's language that stores the alpha-2 code (`UZ`), clearable. With
  `regions`, their countries head the list. The free-text code and its "two-letter code" check are gone.
- A member's `proposeEvent` publishes at once in a private catalog (the member's organization is its only reader) and
  lands as a candidate awaiting review in a shared or public one; there only a reviewer publishes. `EventsFeed` says
  which and reloads to show a published proposal. Its `proposal` prop (`EventProposalOptions`) adds the application
  deadline and "Where it was found" (the website when it has none, and `sourceField` of the tracking), and writes
  `initialTracking` right after the event is created.
- A product embeds a catalog's digests with `<DigestsPanel :client :catalog :locale :defaults :link-template />`:
  `defaults` is a new digest's fields (its schedule, selection, recipients, language), `linkTemplate` fixes each event's
  link to the product's own page, `channels` the channels offered (`['telegram']` by default). It needs `events:admin`.
  The stage pickers (`selection.stages` and `excludeStages`) offer the `workflow` prop's stages, else the workflow the
  client reads (`GET …/workflow`); a client that cannot reach that route keeps the digest's stages without a notice.
- A browser session through Identity's BFF passes `csrfToken: () => session.csrfToken`; the client echoes it as
  `X-Csrf-Token` on every unsafe request.
