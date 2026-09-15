# CritterCrush — built from the raw Event Model, not by hand

This sample is **generated from a declared Event Model** through the first-party Critter Stack
pipeline. Nothing here was written by reading code and imitating it: the model came first, the
scaffold came from the model, and the implementation is filled against Bobcat specs whose
identities the model declared.

## Where it came from

The board is the K9CRUSH Event Model from
[Powerworks/K9DatingApp](https://github.com/Powerworks/K9DatingApp) (MIT) — 28 chapters exported
as raw `emlang` YAML. The **BookingAppointments** chapter was chosen for its shape: six commands,
three automations, and two views exercise every Event Modeling slice pattern.

```
K9CRUSH.emlang.v3.yaml  (raw board export, 161 slices)
        │  bobcat import-event-model
        ▼
models/CritterCrush.emodel.yaml   ← curated: domains, aggregates, tightened triggers
        │  Bobcat.EventModel.Scaffolding
        ▼
CritterCrush/Appointments/*.cs + CritterCrush.Specs/Features/*.feature
        │  the critterstack-sdd skills fill the judgment (36 TODOs)
        ▼
green specs → the Stoat plan's slice gates go done
```

## Reading the current state

The scaffold is **structurally complete and deliberately unfinished**. Every mechanical decision
is made — the aggregate handler workflow, collapsed endpoints, `EventsToAppend` returns,
`ProblemDetails` guards harvested from the model's refusal scenarios, `MultiStreamProjection` for
both fan-out views, one `Appointment` aggregate folding all nine events — and every *judgment*
is a marked `TODO`: field mapping, decision logic, projection identity routing.

That boundary is the point. The generator spends zero tokens on shape; the agent spends them only
on meaning.

## Running it

```bash
docker compose up -d                                    # Postgres on 5433, from the repo root
docker exec -it $(docker ps -qf "publish=5433") psql -U postgres -c "CREATE DATABASE crittercrush;"
dotnet test CritterCrush.Specs                          # specs are MTP tests (Bobcat 0.10.0)
dotnet test CritterCrush.Specs -- --filter-feature BookingAppointments
dotnet run --project CritterCrush
```

The spec project has **no hand-written `Main`** — Bobcat's generator emits the entry point and
calls `[BobcatConfiguration]` (see `SuiteConfiguration.cs`).

## The parked v1

`../CritterCrush.v1-parked/` is the hand-written review vehicle that proved the idioms and the
tooling. It did its job; this is the version the pipeline built.
