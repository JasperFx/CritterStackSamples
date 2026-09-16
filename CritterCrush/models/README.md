# Regenerating CritterCrush from its event model

`CritterCrush.emodel.yaml` is the source of truth. Everything under `CritterCrush/Scheduling/`,
`CritterCrush/Volunteering/`, `CritterCrush.Specs/Features/` and `crittercrush-plan.yaml` is emitted from it by
`Bobcat.EventModel.Scaffolding`, at zero token cost, and is safe to throw away and regenerate —
until a slice is filled in, at which point regenerating that slice's file would overwrite the work.

```bash
dotnet run --project models/Scaffolder -- models/CritterCrush.emodel.yaml out \
    --arrangements --plan models/crittercrush-plan.yaml
cp out/Features/*.feature CritterCrush.Specs/Features/
cp out/Scheduling/*.cs CritterCrush/Scheduling/       # ONLY for slices still unimplemented
cp out/Volunteering/*.cs CritterCrush/Volunteering/   # likewise
```

The runner is `models/Scaffolder`, a few lines around `SliceScaffolder.ScaffoldAll(model)` — use
that single entry point and not the individual `Scaffold`/`ScaffoldAggregates`/`ScaffoldFeatures`
methods, because the pieces are not independent and skipping one leaves a dangling type. It is in
`CritterCrush.sln` deliberately: this repository has no CI, so the only thing standing between a
scaffolding API change and a README that quietly stopped working is that building the solution
compiles it.

⚠️ **`--arrangements` is not optional for this chapter**, whatever its name suggests. The committed
`BookingAppointments.feature` was generated with it (bobcat#259): twelve of sixteen scenarios shared
the same arranged history and it is now three named `@arrangement` scenarios they reference by name.
Regenerate without the flag and that history is silently inlined back into every scenario — a file
that still passes and reads considerably worse. As of 2026-09-16 the command above reproduces all
six committed features **byte for byte**.

⚠️ **Copy `.cs` files back only for slices that are still unimplemented.** Regenerating a filled-in
slice overwrites the work; the `.feature` is the file that is always safe to take.

## `--plan` exists because the last hand-written plan rotted

A Stoat slice node's gate is its spec identities — `{Feature}/{Scenario}`, the exact string a Bobcat
scenario's Uid carries. So a scenario renamed in the model orphans a gate that can then never open,
silently, and the previous hand-written plan had **eleven identities matching no scenario at all**
after the chapter was re-curated. `StoatPlan.From(model)` derives the plan from the same document
the specs come from, so the two cannot disagree.

The dependency edges are derived too, and only two rules produce them, because anything more
mechanical builds a cycle — `ConfirmAppointment` arranges a cancellation in its refusal scenario
while `CancelAppointment` arranges a confirmation in its own:

- a **command** depends on whoever emits the event that *starts* its stream
- an **automation** depends on whoever emits its *trigger* — it has no `given:`, so the rule above
  never sees it, and this is the edge that crosses a chapter boundary
- a **view** depends on whoever emits what it *consumes*

The plan is named for its chapter when the model has exactly one, and for the model when it has
several: the first slice's chapter is not the subject of a two-chapter plan.

Validate the output with Stoat's own strict parser, which rejects unknown keys, dangling edges and
cycles. Note the wire vocabulary is underscored — `depends_on`, not `dependsOn`.

## What the model has to carry that a board export does not

An emlang board export has no field information at all, so a model imported straight from one
scaffolds empty records and scenarios with nothing to drive. `elements:` field hints and scenario
`with:` values are the substance; curating four slices and not the other seven produces four real
slices and seven hollow ones, which is worse than none, because the hollow ones still compile.

Four traps found re-deriving these chapters on Bobcat 0.22.0, every one surfaced by the scaffold or
the specs rather than by the model author:

- **Declare an event's `elements:` on the slice that EMITS it**, not on a slice that merely arranges
  it. Fields declared on the wrong slice are ignored, and the generated record keeps only the keys
  some scenario's `with:` happened to name. `AppointmentCancelled` lost both its routing ids that
  way — visible only because the fan-out projection then reported it could not route the event.
- **The curated format knows ten scalar types** (`Guid`, `int`, `long`, `bool`, `string`, `decimal`,
  `double`, `DateTimeOffset`, `DateOnly`, `TimeSpan`). Anything else — `Dictionary<Guid, string>`,
  say — becomes `string`, silently. There is no collection field in this format; if a projection
  needs per-item state, put what it needs on the event instead, which is where an event-sourced
  design wanted it anyway.
- **A consuming slice needs its trigger's `fields:` declared on it too**, even though the emitting
  slice owns the record. `elements:` resolves per slice, and `CreatesTheStream` reads the trigger's
  fields through that same per-slice lookup — an empty list reads as "identifiable", which flips a
  `StartStream` automation into a `[WriteModel]` bind that fails at dispatch and, because HTTP
  endpoints are discovered eagerly, takes the whole host down with it. bobcat#321. The copy is
  duplication nothing checks, so when a contract gains a field, grep for the type name.
- **A creating command whose stream id is not named `{Aggregate}Id` needs `[Identity]`** (from the
  `JasperFx` namespace). The scaffold recommends it in a comment, which does not boot a host: without
  it Wolverine cannot resolve the aggregate and refuses at discovery, so all 37 scenarios report
  `did not run` for one slice's mistake.

## The diagnostic that earns its keep

`BOBCAT012` caught the one mistake nothing else would have. When Volunteering came into the model,
Scheduling's now-redundant `HomeCheckAssignmentAccepted.cs` was still on disk, so the step text
`HomeCheckAssignmentAccepted is received` resolved to two types — and the generator said exactly
that, naming both namespaces, at build time. Deleting the consumer's stale copy of a contract is
easy to forget, and this is what remembers.

## There used to be a patch step here

Regenerating this chapter and running what came out found eight defects in the scaffolder, and until
they were fixed a `patch_feature.py` sat between the generator and the repository, rewriting what
the scaffolder should have emitted. It is gone as of Bobcat 0.14.0 — every gap it stood in for
closed upstream:

| Issue | What it stood in for |
|---|---|
| [#231](https://github.com/JasperFx/bobcat/issues/231) | a collapsed HTTP slice is driven over HTTP, not the bus |
| [#235](https://github.com/JasperFx/bobcat/issues/235) | `{streamId}` means "the stream this scenario runs against" |
| [#237](https://github.com/JasperFx/bobcat/issues/237) | an HTTP guard refuses with 400 and throws nothing |
| [#241](https://github.com/JasperFx/bobcat/issues/241) | a `Given` may arrange an event partially |

The generation step is now the whole of it: model in, code, specs and plan out, nothing in between.
