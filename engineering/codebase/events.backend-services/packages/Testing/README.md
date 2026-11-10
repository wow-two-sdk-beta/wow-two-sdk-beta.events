# WoW2.Sdk.Beta.Be.Events.Testing

The Events test kit:

- a conformance suite per broker, in `WoW.Two.Sdk.Beta.Be.Events.Testing.Conformance`:
  `EventCatalogsBrokerConformance`, `CatalogEventsBrokerConformance`, `EventReviewBrokerConformance`,
  `EventTrackingBrokerConformance`, `EventWorkflowsBrokerConformance`, `EventImportsBrokerConformance`,
  `EventCalendarBrokerConformance` and `EventIntakeBrokerConformance` (the status's shape only)
- the contract's rules they prove: owner versus another organization (a private catalog's reads answer 404, never
  403), published events only for a reader outside the owner, revision conflicts, review state rules, import id-map
  stability and dry runs, tracking shared inside an organization and invisible outside it
- `TestPrincipal`: a settable `ICurrentPrincipal` — `ActAsMember`, `ActAsService`, `ActAs(EventsActor)`, `ActAsAnonymous`

```bash
dotnet add package WoW2.Sdk.Beta.Be.Events.Testing --prerelease
```

```csharp
// Derive one class per suite and supply the brokers as an actor reaches them.
public sealed class MyCatalogsConformance(MyFixture fixture) : EventCatalogsBrokerConformance
{
    protected override EventsBrokerSet As(EventsActor actor) => fixture.For(actor);
}
```

- an actor is a person of an organization holding every Events scope and permission, or a service with every scope
  and no organization; ids are GUIDs, as Identity's tokens carry them
- in process, `For` opens a scope whose `TestPrincipal` acts as the actor; over HTTP, it sends a token for that
  organization and person (Identity's `FakeTokenIssuer`); each case makes its own organizations and slugs, so no reset
  runs between cases
