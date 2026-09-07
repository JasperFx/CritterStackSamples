# Regenerating CritterCrush from its event model

`CritterCrush.emodel.yaml` is the source of truth. Everything under `CritterCrush/Appointments/`
and `CritterCrush.Specs/Features/` is emitted from it by `Bobcat.EventModel.Scaffolding`, at zero
token cost, and is safe to throw away and regenerate — until a slice is filled in, at which point
regenerating that slice's file would overwrite the work.

```bash
dotnet run --project <scaffolder-runner> -- models/CritterCrush.emodel.yaml out
cp out/Appointments/*.cs CritterCrush/Appointments/
cp out/Features/*.feature CritterCrush.Specs/Features/
```

The runner is eight lines around `SliceScaffolder.ScaffoldAll(model)` — use that single entry
point and not the individual `Scaffold`/`ScaffoldAggregates`/`ScaffoldFeatures` methods, because
the pieces are not independent and skipping one leaves a dangling type.

## There used to be a patch step here

Regenerating this chapter and running what came out found eight defects in the scaffolder, and
until they were fixed a `patch_feature.py` sat between the generator and the repository, rewriting
what the scaffolder should have emitted. It is gone as of Bobcat 0.14.0 — every gap it stood in
for closed upstream:

| Issue | What it stood in for |
|---|---|
| [#231](https://github.com/JasperFx/bobcat/issues/231) | a collapsed HTTP slice is driven over HTTP, not the bus |
| [#235](https://github.com/JasperFx/bobcat/issues/235) | `{streamId}` means "the stream this scenario runs against" |
| [#237](https://github.com/JasperFx/bobcat/issues/237) | an HTTP guard refuses with 400 and throws nothing |
| [#241](https://github.com/JasperFx/bobcat/issues/241) | a `Given` may arrange an event partially |

The generation step is now the whole of it: model in, code and specs out, nothing in between.

## What the model has to carry that a board export does not

An emlang board export has no field information at all, so a model imported straight from one
scaffolds empty records and scenarios with nothing to drive. `elements:` field hints and scenario
`with:` values are the substance; curating four slices and not the other seven produces four real
slices and seven hollow ones, which is worse than none, because the hollow ones still compile.
