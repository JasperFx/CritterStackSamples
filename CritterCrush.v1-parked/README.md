# CritterCrush

A dog-dating sample built **spec-first from a declared Event Model**, in full Critter Stack house
style. The original is [Powerworks/K9DatingApp](https://github.com/Powerworks/K9DatingApp) (MIT) —
a third-party app generated from an eventmodelers.ai board; CritterCrush rebuilds it through the
first-party pipeline: a curated Event Model file → Bobcat specs bound by identity → Wolverine +
Marten slices, with drift between the declared model and the code rendered by the Bobcat viewer.

**Iteration 1** carries the two flows the original descoped (dog profiles, swipe → mutual match),
as five Event Modeling slices:

| Slice | Pattern | Where |
|---|---|---|
| CreateDogProfile | Command | `CritterCrush/Profiles/CreateDogProfile.cs` |
| ViewDogProfile | View | `CritterCrush/Profiles/GetDogProfile.cs` |
| SwipeOnDog | Command | `CritterCrush/Discovery/SwipeOnDog.cs` |
| DetectMutualMatch | Automation | `CritterCrush/Discovery/DetectMutualMatch.cs` |
| MatchList | View | `CritterCrush/Discovery/MatchList.cs` |

## The idioms on display

- **Aggregate handler workflow everywhere**: `[WriteModel]`/`[ReadAggregate]` parameters, pure
  handlers returning `EventsToAppend` / `IStartStream` / cascaded messages — no injected session,
  no `SaveChangesAsync`, `AutoApplyTransactions` owns the commit.
- **The endpoint IS the handler**: command slices are collapsed — one transaction appends the
  events (`IStartStream` / `EventsToAppend` returns), the outbox carries anything cascaded, and
  the status code is honest. `SwipeRequest` shows `[Identity]` on a computed property binding
  the deterministic pair-stream id straight off the body. A separate message handler exists only
  when a command genuinely needs bus visibility — never for testability.
- **The automation slice discipline**: `SwipeOnDog` decides only "is this swipe recordable"; the
  mutual-match consequence is `DetectMutualMatch`, triggered by the `DogLiked` event via fast
  event forwarding, aggregating its own stream (`[WriteModel]` hides the aggregation), idempotent
  under redelivery.
- **Lifecycles chosen per read pattern**: write models as Inline snapshots (your next GET sees
  your write); the `MatchList` fan-out as an Async multi-stream projection with the daemon
  actually running (`AddAsyncDaemon(DaemonMode.Solo)`).
- **Modular-monolith rails on from day one**: `MultipleHandlerBehavior.Separated`,
  `MessageIdentity.IdAndDestination`, durable local queues.
- **Specs are the tests, driven over HTTP**: `CritterCrush.Specs` is xUnit v3, every test a Bobcat
  projected specification (`[BobcatSpec]`) on `CritterStackSpec`. The collapsed slices are driven
  with Alba inside a Wolverine tracked session (`CritterCrushSpec.WhenPosted`), so even the
  automation cascade settles before assertions run. Test names reproduce the scenario identities
  declared in [`models/CritterCrush.emodel.yaml`](models/CritterCrush.emodel.yaml) exactly, which is
  what binds run evidence onto the Event Model.

## Running it

```bash
# Postgres on 5433, from the repo root
docker compose up -d
docker exec -it $(docker ps -qf "publish=5433") psql -U postgres -c "CREATE DATABASE crittercrush;"

# The specs: xUnit v3 on Microsoft.Testing.Platform (global.json selects it for dotnet test)
dotnet test

# The same run, with every specification rendered step by step into specs.txt
BOBCAT_SPEC_CONSOLE=1 BOBCAT_SPEC_CONSOLE_FILE=specs.txt ./CritterCrush.Specs/bin/Debug/net10.0/CritterCrush.Specs

# The app itself
dotnet run --project CritterCrush
```

Until Bobcat 0.50 the specs were Gherkin (`Features/*.feature`) run by a self-executing Bobcat suite
(`dotnet run --project CritterCrush.Specs`). That lane is gone (bobcat#471); each scenario is now a
`[BobcatSpec]` test of the same name in `CritterCrush.Specs/Specs/`.

## The Event Model

[`models/CritterCrush.emodel.yaml`](models/CritterCrush.emodel.yaml) is the declared model in the
curated format, kept as the record of what was declared. Bobcat 1.0 no longer reads it: the curated
format is retired as an authoring surface (bobcat#406), and `bobcat import-event-model` now takes an
eventmodelers.ai board export and refuses a curated file. A declared model would today be an
`EventModelDefinition` in C#, registered with `AddEventModel<T>()` (see `../CritterCrush`). The specs'
generated source still contributes the slices from the code side under the model name `CritterCrush`.
