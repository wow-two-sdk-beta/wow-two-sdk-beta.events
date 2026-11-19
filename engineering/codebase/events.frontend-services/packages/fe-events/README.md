# @wow-two-beta/fe-events

The Events domain's UI: a shared catalog of dated happenings (fairs, fashion weeks, competitions) curated in review,
with each organization's private tracking. Sourced records serve internal tracking only: sourced images and text are
review evidence, never content.

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
| `./contracts` | The TypeScript mirror of the be contract (camelCase JSON, enums as camelCase strings, localized text as `Record<locale, string>`), its constants and `EventsRoutes`, the route builders, with the query mappers |
| `./kit` | `createEventsClient` (every route, `{ data }` unwrapped, failures mapped by status), date helpers honest about precision and certainty (`formatEventWhen`, `formatEventSpan`, `groupEventsByMonth`, `sortEventsByDate`), `downloadEventsCalendar` and one event's `createEventCalendar`, the title, price, region and source helpers (`pickTextEntry`, `eventTitleLanguage`, `formatOfferPrice`, `regionQuery`, `listingRemoved`), and `EventsFeed`, `EventCard`, `EventsDeadlines`, `EventDetail`, `EventProposalForm`, `TrackingForm` |
| `./admin` | The reviewer's and admin's screens: `ReviewQueue`, `ObservationsList`, `ObservationEvidence`, `IntakeStatusPanel`, `CatalogEventsTable`, `ImportPanel`, and scheduled digests: `DigestsPanel` (list, editor, run history), `DigestEditor`, `DigestRunHistory`, `DigestRecipients`, `TelegramText` |

- Dates never claim more than they know: a month stays a month (`Oct 2026`), an estimate reads `≈ Oct 2026 (expected)`
  even when it names a day or a time, an unknown date `Dates not announced`, and undated events sort last. A start time
  names its zone (`19:00 GMT+5`) only when the event's zone runs at another offset than the viewer's.
- Titles match by language prefix (`ru` finds `ru-Cyrl`, `uz` finds `uz-Latn`); a card and the detail tag a title written
  in another language (`RU`).
- An organization's page lists its tracking with `<EventsFeed tracking :hidden-stages :regions :quick-stages :can-track
  :shown-fields />`: each card carries its stage (an untracked event the workflow's default stage, marked "New — review"),
  priority and chosen `tracking.fields`; a stage filter counts each stage; `hiddenStages` are left out unless the
  "Show …" toggle or their own filter asks; `regions` (`{ key, label, countries | otherCountries }`) replace the country
  input with chips over country sets and show each event's scope; `quickStages` move an event at the default stage in one
  click. `EventsDeadlines` takes `deadlineKinds` and `eventHref`; `EventDetail` takes `trackable`, `regions`,
  `shownFields`, `hiddenFields` and offers the event's own calendar file; `TrackingForm` keeps the draft on a 409, emits
  `dirty`, and shows a schema field marked `readOnly` without offering it. Mark a stage `lost` in the workflow to keep
  its events out of the deadlines.
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
