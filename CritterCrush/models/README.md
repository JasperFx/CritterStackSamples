# Regenerating CritterCrush from its event model

`CritterCrush.emodel.yaml` is the source of truth. Everything under `CritterCrush/Appointments/`
and `CritterCrush.Specs/Features/` is emitted from it by `Bobcat.EventModel.Scaffolding`, at zero
token cost, and is safe to throw away and regenerate — until a slice is filled in, at which point
regenerating that slice's file would overwrite the work.

```bash
dotnet run --project <scaffolder-runner> -- models/CritterCrush.emodel.yaml out
python3 models/patch_feature.py models/CritterCrush.emodel.yaml \
        out/Features/BookingAppointments.feature out/Appointments
cp out/Appointments/*.cs CritterCrush/Appointments/
cp out/Features/*.feature CritterCrush.Specs/Features/
```

The runner is eight lines around `SliceScaffolder.ScaffoldAll(model)` — use that single entry
point and not the individual `Scaffold`/`ScaffoldAggregates`/`ScaffoldFeatures` methods, because
the pieces are not independent and skipping one leaves a dangling type.

## Why there is a patch step at all

`patch_feature.py` is scaffolding the scaffolder does not do yet. Every rewrite in it is
mechanical, derived from the model, and tagged with the Bobcat issue that will delete it:

| Issue | What the patch stands in for |
|---|---|
| [#231](https://github.com/JasperFx/bobcat/issues/231) | a collapsed HTTP slice is driven over HTTP, not the bus |
| [#235](https://github.com/JasperFx/bobcat/issues/235) | `{streamId}` means "the stream this scenario runs against" |
| [#237](https://github.com/JasperFx/bobcat/issues/237) | an HTTP guard refuses with 400 and throws nothing |
| [#241](https://github.com/JasperFx/bobcat/issues/241) | a `Given` may arrange an event partially |

When those close, delete the corresponding branch — and when all four have, delete the file.

## What the model has to carry that a board export does not

An emlang board export has no field information at all, so a model imported straight from one
scaffolds empty records and scenarios with nothing to drive. `elements:` field hints and scenario
`with:` values are the substance; curating four slices and not the other seven produces four real
slices and seven hollow ones, which is worse than none, because the hollow ones still compile.
