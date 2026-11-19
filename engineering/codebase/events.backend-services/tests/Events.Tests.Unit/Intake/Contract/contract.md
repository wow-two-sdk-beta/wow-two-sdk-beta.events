# Contract/

SCFD's golden pages of the pull wire Events reads, copied unchanged from harvest, and the tests that replay them.

- `wow2.event.v1/` is harvest's `tests/Contract/wow2.event.v1/` byte for byte: one `GET api/subscriptions/{id}/events`
  answer per file, `subscription.json` (the subscription's own `GET`) and `manifest.json` (`sha256` and case names per
  file). Harvest's `EventContractPagesTests` fails on the first byte its host serves differently, so the files are the
  wire. Never edit one here.
- `wow2.event.v1/selector/` is harvest's `selector/` folder byte for byte: a selector subscription's `subscription.json`,
  `01-match.json` (`post.new`, kind from `data.context.defaultKind`), `02-change.json` (`post.unmatched` after a selector
  change) and its own `manifest.json`. `SelectorPages_ShouldMapKindFromThePayloadContext` (unit) and
  `SelectorReplay_ShouldPublishTheMatch_AndHoldTheUnmatchedAfterTheSelectorChange` (integration) read it.
- `source.json` records the harvest tag and commit the folder was copied from.
- `ScfdContractPagesTests` (unit) checks every file against the manifest, reads each page through the real
  `ScfdObservationFeed`, and pins what Events reads from it: facts, place, time zone, lifecycle state, classification,
  purge, the application deadline (`09-deadline.json`), the unmatched post (`10-unmatched.json`) and the estimated date
  (`11-estimated.json`: an undated post, an `estimated` month read as approximate, then the stated day read as announced;
  every other page's stated dates keep their precision as announced) and the English title (`12-english-title.json`:
  SCFD's machine `en` title maps beside the source's `ru-Cyrl`, recorded as machine-made on the title's link).
- `ScfdContractReplayTests` (integration) pulls the pages in order through the real intake over PostgreSQL, sending each
  page's `next` as the following cursor.
- Events' own expectations live in the tests; the numeric enum wire harvest no longer serves keeps a hand-made fixture
  in `ScfdEventMapperTests`.

## Re-copy

After a harvest release that changes the folder, from `events.backend-services/`, with the tag in `TAG` and harvest's
checkout in `HARVEST`:

```bash
git -C "$HARVEST" archive "$TAG" engineering/codebase/harvest.backend-services/tests/Contract/wow2.event.v1 | tar -x --strip-components=5 -C tests/Events.Tests.Unit/Intake/Contract
```

Then set `tag`, `commit` (`git -C "$HARVEST" rev-parse "$TAG^{commit}"`) and `copiedOn` in `source.json`, run both test
classes, and change an expectation only for a wire change harvest made on purpose.
