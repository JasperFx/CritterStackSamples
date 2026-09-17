# Where this is, for tomorrow

**Open in Rider:** `~/code/crittercrush-clean/CritterCrush/CritterCrush.sln`
Branch `regen-clean-0.26.2`, 2 commits, **nothing pushed**. 88 specs green in ~47s.

Run the suite: `docker compose up -d` is NOT needed — Postgres on **5433** is Wolverine's own
container, shared. Then:

```bash
cd ~/code/crittercrush-clean/CritterCrush
dotnet build CritterCrush.sln -c Release
./CritterCrush.Specs/bin/Release/net10.0/CritterCrush.Specs
```

`dotnet test` works too but hides the per-test output; the exe is the honest read.

## What this repo is

CritterCrush regenerated **clean** on Bobcat 0.26.2: fresh clone of `origin/main`, the curated model
+ plan + manifest carried in as inputs, scaffolder run once, output dropped in untouched, then only
the decisions filled. **Zero structural edits** — nothing the scaffolder emitted had to be replaced.

Two other working copies, neither pushed, both reference only:
- `~/code/crittercrush-regen` — the 0.26.1 pass that FOUND the defects (hand-fixed projections).
- `~/code/crittercrush-lane-a` — the original pass, pre-0.26. `LANE-A-REVIEW.md` there is stale.

## Worth reviewing first

- `CritterCrush/Scheduling/AppointmentsQueue.cs` and `MyAppointments.cs` — the marker-interface +
  single `Evolve` shape, and the only hand decision in them is which bucket each arm moves.
- Any endpoint, e.g. `ConfirmAppointment.cs` — guard as a `switch` whose `_` refuses, `[EmptyResponse]`,
  no `[ReadModel]`, non-nullable `[WriteModel]`, and the scaffolder's own prose saying there is no
  404 guard to write.
- `CritterCrush.Specs/CritterCrushSpec.cs` — the step vocabulary. **Parameter names must match the
  `[BobcatStep]` placeholders** or the step renders as `{event}`; BOBCAT027 warns, and we ignored it
  for hours. `@event` and `command` are named that way on purpose.

## Shipped today

Bobcat **0.26.2**, all 14 packages live. ai-skills **#208** merged (the guard rule).
Issues: #344/#345/#346/#347 fixed in 0.26.1; #349/#351 fixed in 0.26.2; **#350 closed as invalid**
(BOBCAT027 already existed — I had filtered the build for `error` and missed the warning).

## The two open questions for tomorrow

Both are things neither the model nor the scaffolder can express today, and both were the last
remaining hand work after the ten 404s were declared:

1. **`[property: Identity]` on seven command records.** The stream id is a field of the command
   (`ApplicantOwnerId`, `HomeCheckId`) and nothing in the curated format says which. The scaffold's
   own comment points at the fix, so it is documented rather than automated. Question: can the model
   state it, or is it genuinely per-slice authoring?

2. **Status vocabularies are hand-written files.** The curated format knows only
   `string, Guid, bool, int, decimal, DateTimeOffset`, so every status is `string` and
   `AppointmentStatus` / `HomeCheckStatus` / `VolunteerApplicationStatus` / `AppointmentKind` are
   introduced by hand. Jeremy called enum-over-const-string minor, but this is the format limitation
   propagating into the domain.

## Decision waiting on you

Which working copy becomes the version that goes to `CritterStackSamples` for the blog post —
`crittercrush-clean` is the honest answer (it is what generation produces), and it is the one open
in Rider.
