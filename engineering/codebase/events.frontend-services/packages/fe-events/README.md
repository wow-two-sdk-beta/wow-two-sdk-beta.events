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
| `./kit` | `createEventsClient` (every route, `{ data }` unwrapped, failures mapped by status), date helpers honest about precision and certainty (`formatEventWhen`, `formatEventSpan`, `groupEventsByMonth`, `sortEventsByDate`), `downloadEventsCalendar`, and `EventsFeed`, `EventCard`, `EventsDeadlines`, `EventDetail`, `EventProposalForm`, `TrackingForm` |
| `./admin` | The reviewer's screens: `ReviewQueue`, `ObservationsList`, `ObservationEvidence`, `IntakeStatusPanel`, `CatalogEventsTable` |

- Dates never claim more than they know: a month stays a month (`Oct 2026`), an estimate reads `≈ Oct 2026 (expected)`,
  an unknown date `Dates not announced`, and undated events sort last.
- A member's `proposeEvent` lands as a candidate awaiting review; only a reviewer publishes.
- A browser session through Identity's BFF passes `csrfToken: () => session.csrfToken`; the client echoes it as
  `X-Csrf-Token` on every unsafe request.
