# The scaffold, exactly as Bobcat 0.26.2 emitted it

Nothing here is edited. `../CritterCrush` is this, with the decisions filled in — so any difference
between a file here and its twin there is **ours**, and anything you dislike *in this folder* is the
scaffolder's.

Regenerate it with:

```bash
cd CritterCrush
dotnet run --project models/Scaffolder -c Release -- \
  models/CritterCrush.emodel.yaml ../.scaffold-output --arrangements \
  --plan models/crittercrush-plan.yaml --manifest models/CritterCrush.spec-ownership.yaml
```

Not in the solution, not compiled — it exists to be read beside the real thing.
