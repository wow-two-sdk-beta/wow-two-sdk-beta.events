# flows/

User and product flows; each flow gets its own `{flow-slug}.md` when it needs more than these lines.

| Flow | Steps |
|---|---|
| Source to catalog | SCFD captures a page → delivers a post in `wow2.event.v1` → Events pulls it as an observation → a new post becomes a candidate → a reviewer accepts, edits, merges or rejects it in the console → published events reach products |
| Source correction | a newer revision of a published event's post arrives → a pending change beside the event with its evidence → a reviewer applies or dismisses it; tracking and notes never change |
| Tracking | a product's member opens its Events page → the product's backend calls Events for the person (token exchange) → reads published events with the organization's tracking → moves a stage, sets priority, writes notes |
| Migration | a product exports its own events as `wow2.events-import.v1` → dry run → import with the id map → reconcile → the product freezes its old writer |
