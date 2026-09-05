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
- **HTTP endpoints as pure translations**: `POST /api/discovery/swipes` computes the pair's
  deterministic stream id and *cascades* the command through the transactional outbox — no
  mediator hop, nothing a crash can tear in half.
- **The automation slice discipline**: `SwipeOnDog` decides only "is this swipe recordable"; the
  mutual-match consequence is `DetectMutualMatch`, triggered by the `DogLiked` event via fast
  event forwarding, aggregating its own stream (`[WriteModel]` hides the aggregation), idempotent
  under redelivery.
- **Lifecycles chosen per read pattern**: write models as Inline snapshots (your next GET sees
  your write); the `MatchList` fan-out as an Async multi-stream projection with the daemon
  actually running (`AddAsyncDaemon(DaemonMode.Solo)`).
- **Modular-monolith rails on from day one**: `MultipleHandlerBehavior.Separated`,
  `MessageIdentity.IdAndDestination`, durable local queues.
- **Specs are the tests**: `CritterCrush.Specs` is a Bobcat suite in the shipped grammar —
  `@slice:`/`@domain:` tagged, feature/scenario names reproducing the identities declared in
  [`models/CritterCrush.emodel.yaml`](models/CritterCrush.emodel.yaml) exactly, which is what
  binds run evidence onto the Event Model.

## Running it

```bash
# Postgres on 5433, from the repo root
docker compose up -d
docker exec -it $(docker ps -qf "publish=5433") psql -U postgres -c "CREATE DATABASE crittercrush;"

# The specs — a self-executing Bobcat suite
dotnet run --project CritterCrush.Specs

# The app itself
dotnet run --project CritterCrush
```

## The Event Model

[`models/CritterCrush.emodel.yaml`](models/CritterCrush.emodel.yaml) is the declared model in the
curated format. With a Bobcat console 0.9.2+ (`bobcat import-event-model models/CritterCrush.emodel.yaml
--url http://localhost:5525`) it renders on the Event Model canvas; the specs' generated source
contributes the same slices from the code side, and the two merge by slice name — declared-only
slices are the backlog, spec-bound green slices are done. Status is derived, never asserted.
