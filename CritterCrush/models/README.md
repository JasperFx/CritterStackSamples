# Regenerating CritterCrush from its event model

`CritterCrush.emodel.yaml` is the source of truth, and `CritterCrush.spec-ownership.yaml` says how
each slice is specified — here, every one of them in the projected lane. Everything under
`CritterCrush/Scheduling/`, `CritterCrush/Volunteering/`, `CritterCrush.Specs/Specs/` and
`crittercrush-plan.yaml` is emitted from those two by `Bobcat.EventModel.Scaffolding`, at zero token
cost. It is safe to throw away and regenerate until a slice is filled in; from then on, regenerating
that slice's file overwrites the work.

Every slice here is filled in, so regenerate into the pristine copy at the repository root and
compare, rather than over the implementation:

```bash
dotnet run --project models/Scaffolder -- models/CritterCrush.emodel.yaml ../.scaffold-output \
    --plan models/crittercrush-plan.yaml \
    --manifest models/CritterCrush.spec-ownership.yaml
git diff --stat ../.scaffold-output    # what this Bobcat changed about the output
../review.sh Scheduling/ConfirmAppointment.cs    # one scaffold file beside its implementation
```

Copy a file from `.scaffold-output/` into `CritterCrush/` or `CritterCrush.Specs/Specs/` **only** for
a slice that is still unimplemented.

The runner is `models/Scaffolder`, a few lines around `SliceScaffolder.ScaffoldAll(model)` — use
that single entry point and not the individual `Scaffold`/`ScaffoldAggregates`/`ScaffoldFeatures`
methods, because the pieces are not independent and skipping one leaves a dangling type. It is in
`CritterCrush.sln` deliberately: this repository has no CI, so the only thing standing between a
scaffolding API change and a README that quietly stopped working is that building the solution
compiles it.

As of Bobcat 0.27.3 the command above reproduces all 33 committed files in `.scaffold-output/`
**byte for byte**.

`--arrangements` is no longer part of it. The flag extracts history that scenarios repeat into
named `@arrangement` scenarios (bobcat#259), and that only shapes `.feature` output. With every
slice projected, the output is identical with and without it, measured on 0.27.3. Put it back if a
slice ever moves to the Gherkin lane.

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
  it Wolverine cannot resolve the aggregate and refuses at discovery, so the shared host never
  starts and all 88 tests fail on the collection fixture for one slice's mistake. Seven
  Volunteering commands need it here.

## The diagnostic that earns its keep

`BOBCAT012` caught the one mistake nothing else would have. When Volunteering came into the model,
Scheduling's now-redundant `HomeCheckAssignmentAccepted.cs` was still on disk, so the step text
`HomeCheckAssignmentAccepted is received` resolved to two types — and the generator said exactly
that, naming both namespaces, at build time. Deleting the consumer's stale copy of a contract is
easy to forget, and this is what remembers.

`BOBCAT028` does the same job for the projected lane. A class that binds a slice with
`[BobcatSlice]` but never opens a recording renders as no specification at all, and at run time
that is indistinguishable from a class with nothing to record: every test green, and nothing on
the canvas. The attribute that opens the recording is `[BobcatScenario]`, and 0.27.2's scaffold
writes it on every spec class.

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
