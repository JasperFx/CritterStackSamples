# CritterCrush — built from the raw Event Model, not by hand

This sample is **generated from a declared Event Model** through the first-party Critter Stack
pipeline. Nothing here was written by reading code and imitating it: the board came first, the
curated model came from the board, the scaffold came from the model, and the implementation was
filled against Bobcat specs whose identities the model declared.

## Where it came from

The board is the K9CRUSH Event Model from
[Powerworks/K9DatingApp](https://github.com/Powerworks/K9DatingApp) (MIT) — 28 chapters and 161
slices of raw `emlang` YAML. **Two** of those chapters are built here:

| Chapter | Slices | Why this one |
|---|---|---|
| **BookingAppointments** | 6 commands, 3 automations, 2 views | Exercises every Event Modeling slice pattern between them |
| **VolunteeringAndHomeChecks** | 7 commands, 1 view | It *emits the event BookingAppointments waits for*. With both in the model, that trigger stops being an integration contract and becomes a link — which is the only way to exercise cross-chapter structure at all |

```
Spec/K9CRUSH.emlang.v3.2026-07-31.yaml   (the board's own export, 161 slices)
        │  bobcat import-event-model      ← 11 + 8 slices, no warnings
        ▼
models/CritterCrush.emodel.yaml           ← CURATED: the aggregate, the field shapes, the
        │                                    routing ids, the edge cases, the hotspots
        │  models/Scaffolder  (Bobcat.EventModel.Scaffolding 0.30.0, the last one published)
        ▼
CritterCrush/{Scheduling,Volunteering}/    ← 30 files: every mechanical decision made,
CritterCrush.Specs/Specs/*Specs.cs           every judgment a named TODO
models/crittercrush-plan.yaml             ← the Stoat plan, derived from the same model
        │  the critterstack-sdd skills fill the judgment
        ▼
52 projected specs + 36 invariant checks green
```

**On Bobcat 1.0 the curated file is no longer what the application registers.** Bobcat retired the
curated YAML format as an authoring surface ([bobcat#406](https://github.com/JasperFx/bobcat/issues/406)):
`AddEventModelFile` is gone, and so is `Bobcat.EventModel.Scaffolding` (slice scaffolding is Wolverine's
`scaffold` command now). The model's slice-level declarations (pattern, chapter, trigger, aggregate,
events, views, external systems, hotspots) are therefore ported to C# in
`CritterCrush/CritterCrushEventModel.cs`, two `EventModelDefinition`s registered with
`AddEventModel<T>()`. The YAML stays as the scaffolder's input only, and the scaffolder stays pinned to
the last versions that read it; see `models/README.md`.

The board export is **not** committed here. It belongs to the upstream repository and is fetched
when the model is re-derived, so this repository never carries a stale copy of somebody else's
design.

## What the import knew, and what curation had to decide

The import got all eleven slices, all three automation patterns, and — because the board says so in
prose — which flow triggers each one. It knew nothing else, and said so: the board's own comment
records that its props were *"intentionally omitted rather than invented"*.

So the substance of `CritterCrush.emodel.yaml` is curation, and four decisions in it are worth
reading before the code:

| Decision | Why |
|---|---|
| **One purpose-tagged `Appointment`**, `kind` + `sourceId` | The board asked for it in prose: *"a single generic, purpose-tagged Appointment concept … reused across three automation entry points rather than three separate booking flows"* |
| **`ownerId` and `shelterId` on every event** | Both views fold many appointment streams into one document. A multi-stream projection can only route an event that carries the id it groups by — fan-out routing constrains event shape |
| **The appointment's stream id IS the thing that asked for it** | An assignment, a foster application or a surrender request. Nothing upstream invents an appointment id, and a redelivered trigger collides on `StartStream` instead of quietly booking a second visit |
| **`wasConfirmed` on `AppointmentCancelled` alone** | It is the one closing event reachable from either state, and a counting projection has no prior state to consult. The aggregate knows, so the aggregate says |

Three questions the board could not settle are **hotspots** on the model rather than guesses in the
code: whether a rejected surrender review should still book an intake, whether a shelter may move an
appointment nobody asked to move, and what happens when one source entity needs a second visit.

## Reading the current state

Nineteen slices across two chapters, every one specified and built. The specs are xUnit v3 tests,
every one a Bobcat projected specification (`[BobcatSpec]`, bound to its slice), plus one 36-row
theory in `Invariants/` that checks the shelter queue's arithmetic for every state × command pair and
is deliberately not a specification. Two of the thirty scaffolded files ship exactly as the generator
emitted them — the inbound integration contracts for the two trigger events no chapter here owns —
and two files are hand-written additions the scaffold has no opinion about (the shared 404 refusals).

Two of those scenarios are worth singling out, because each was deliberately broken to prove it can
fail before it was believed:

- **`An owner's page spans every appointment stream they have`** is the fan-out check. Route one of
  the projection's identity rules to the wrong key and this scenario alone goes red
  (`AwaitingConfirmation: expected 1, was 0`). It arranges two streams in one scenario
  (`GivenEventsOn<Appointment>` beside `GivenEvents<Appointment>`).
- **`An appointment cancelled before anyone confirmed it leaves the awaiting count`** is what makes
  `wasConfirmed` load-bearing. Decrement the naive counter instead and this scenario alone goes red
  (`Confirmed: expected 0, was -1`).
- **`Accepting an assignment books the home check as an appointment`** is the only scenario that
  crosses the chapter boundary at runtime: a POST to Volunteering, and an assertion on an
  Appointments read model two hops later. Put `[WolverineIgnore]` on the appointments automation and
  exactly two scenarios redden — this one, and the automation's own.

## Where the two chapters meet

`AcceptHomeCheckAssignment` (Volunteering) emits `HomeCheckAssignmentAccepted`;
`ProposeHomeCheckAppointment` (Scheduling) handles it. Because both chapters are in one model the
trigger resolves as **Emitted** rather than **Inbound**, so the emitting slice owns the record and
the consuming slice declares no external system — the scaffolder stops writing a second copy of the
contract, and the cross-namespace reference in `GlobalUsings.cs` is the code-side shape of the link.

One decision had to change shape to survive this. BookingAppointments makes an appointment's stream
id the source entity's id, which is what lets its automations be specified at all — and Marten's
stream id space is **global across aggregate types**, so the moment a `HomeCheck` stream took the
same id, `StartStream<Appointment>` would collide. Hence `assignmentId` is the *assignment's* own
identity, minted when a volunteer accepts, and not the home check's stream id. An assignment is
genuinely its own thing (the board draws "Home Check Assigned" as its own step, and a reassignment is
a second assignment), and the id is fixed once written, so redelivery still lands on one appointment.

The two chapters also demonstrate the two **View** shapes on purpose: Appointments' two views are
multi-stream fan-outs keyed by an owner and a shelter, while Volunteering's one view is
single-stream, one document per application folded from that application's own stream.

## Running it

```bash
docker compose up -d                                    # Postgres on 5433, from the repo root
dotnet test                                             # from this folder: 88 tests, 52 of them specs
dotnet run --project CritterCrush
```

The database is created on start-up if it is missing (`CreateDatabasesForTenants` in `Program.cs`).

`global.json` here selects Microsoft.Testing.Platform for `dotnet test`, which xunit.v3 4.x on
Microsoft.Testing.Platform 2 requires on the .NET 10 SDK; without it `dotnet test` refuses to run the
project. The spec project is also an executable, and running it directly works too:

```bash
BOBCAT_SPEC_CONSOLE=1 BOBCAT_SPEC_CONSOLE_FILE=specs.txt ./CritterCrush.Specs/bin/Debug/net10.0/CritterCrush.Specs
```

writes every specification, step by step, to `specs.txt`. xUnit owns the entry point: there is no
generated `Main` and no `[BobcatConfiguration]` any more (Bobcat.Mtp and its runner are gone in 1.0).
The host is `CritterCrush.Specs/CritterCrushHost.cs`.

## The parked v1

`../CritterCrush.v1-parked/` is the hand-written review vehicle that proved the idioms and the
tooling. It did its job; this is the version the pipeline built.
