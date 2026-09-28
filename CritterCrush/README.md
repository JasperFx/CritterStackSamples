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
models/CritterCrush.spec-ownership.yaml   ← how each slice is specified: here, all projected
        │  models/Scaffolder  (Bobcat.EventModel.Scaffolding)
        ▼
CritterCrush/{Scheduling,Volunteering}/    ← 33 files: every mechanical decision made,
CritterCrush.Specs/Specs/*.cs                every judgment a named TODO that compiles and throws
models/crittercrush-plan.yaml             ← the Stoat plan, derived from the same model
        │  the critterstack-sdd skills fill the judgment
        ▼
52 scenarios green
```

The board export is **not** committed here. It belongs to the upstream repository and is fetched
when the model is re-derived, so this repository never carries a stale copy of somebody else's
design.

## What the import knew, and what curation had to decide

The import got all eleven BookingAppointments slices, all three automation patterns, and — because the board says so in
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

Nineteen slices across two chapters, every one specified and built: **52 scenarios green**, 88 tests
in all. The other 36 are `AppointmentQueueInvariants`, an ordinary xUnit theory over every
state-and-command pair; it carries no `[BobcatFeature]`, so it runs in the same suite and publishes
no specification.

Every slice is specified in the **projected** lane, which `models/CritterCrush.spec-ownership.yaml`
states once for the whole model: each scenario is an ordinary xUnit test, bound to its slice with
`[BobcatSlice]`, whose steps render from the `[BobcatStep]` helpers in `CritterCrushSpec.cs`. There
are no `.feature` files. The step text binds at run time, so a rendered step carries the stream id
and route that scenario actually used.

The scaffold is committed as it was emitted, at the repository root in `.scaffold-output/`, and
`../review.sh <path>` puts any file beside its implemented twin. Any difference between the two is a
decision somebody made here; anything objectionable in `.scaffold-output/` is the scaffolder's. It
regenerates from the model **byte for byte** — see `models/README.md`.

What the scaffold does not write, and was written by hand: `CritterCrushHost.cs` (the one host every
spec class shares), `CritterCrushSpec.cs` (the step vocabulary), the invariants, `Program.cs`,
`GlobalUsings.cs` and `AppointmentKind.cs`. The status vocabularies (`AppointmentStatus`,
`HomeCheckStatus`, `VolunteerApplicationStatus`) were added inside the scaffolded aggregate files,
because the curated format has no enum type. Seven Volunteering commands also needed
`[property: Identity]` by hand, since nothing in the format says which field is the stream id.

Three of those scenarios are worth singling out, because each was deliberately broken to prove it
can fail before it was believed:

- **`An owner page spans every appointment stream they have`** is the fan-out check. Key the owner
  view by the wrong id and it goes red, along with the two other scenarios that read that view. It
  needs `stream:` in the curated `given:`
  ([bobcat#311](https://github.com/JasperFx/bobcat/issues/311), Bobcat 0.21.0), and `GivenEventsOn`
  in the test, to arrange two streams in one scenario at all.
- **`An appointment cancelled before anyone confirmed it leaves the awaiting count`** is what makes
  `wasConfirmed` load-bearing. Decrement the naive counter instead and this is the one specification
  that goes red (`AwaitingConfirmation: expected 0, was 1`), along with the seven invariant cases
  that pass through a cancellation.
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
docker compose up -d        # Postgres on 5433, from the repository root
cd CritterCrush
dotnet test                 # all 88 tests
dotnet run --project CritterCrush
```

There is no database to create: the app creates `crittercrush` on first use and applies its schema
on startup. The spec classes share one host through an xUnit collection and run one at a time,
because each test resets the event store and two resetting concurrently would wipe each other's
arranged history. xUnit owns the entry point (`BobcatGenerateEntryPoint=false`), so `dotnet test`
and running the `CritterCrush.Specs` executable directly are equivalent.

**A run prints no specification.** The steps are published to a monitor, and the receiver is
[Stoat](https://stoat.jasperfx.net), listening on `http://localhost:5525` by default. With a Stoat
console running, the suite publishes each scenario's steps to it, and this publishes the Event Model
Wolverine reads out of the code:

```bash
dotnet run --project CritterCrush -- event-model --url http://localhost:5525/api/event-model
```

## The parked v1

`../CritterCrush.v1-parked/` is the hand-written review vehicle that proved the idioms and the
tooling. It did its job; this is the version the pipeline built.
