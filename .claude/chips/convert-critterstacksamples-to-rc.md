# Title

Convert CritterStackSamples to the Critter Stack 2026 RC matrix — dogfood the migration skills

## TL;DR

Convert the CritterStackSamples repo (13 independent solutions, 33 projects, currently Marten 8.29–8.31 / Wolverine 5.29–5.31, some still `net8.0`) to the Critter Stack 2026 RC pin matrix, applying the two migration skills (`marten-migration-v8-to-v9`, `wolverine-migration-v5-to-v6`). This is a **dogfooding** exercise as much as a conversion — every place where a needed code change is NOT covered by the skills is a defect to report back. Pilot 2–3 solutions first (one mechanical + the API-touching ones) to shake out skill gaps before sweeping the rest. Do NOT push; end with a per-solution status table + a skill-gap report.

## Prompt

**Repo: CritterStackSamples** (https://github.com/JasperFx/CritterStackSamples). Root: `/Users/jeremymiller/code/CritterStackSamples`. Base branch: `main`. Work on a branch `chore/rc-conversion`; commit per solution; **do NOT push** — the user opens the PR.

If your worktree is rooted anywhere other than the CritterStackSamples repo, STOP and report — do not attempt cross-repo edits (reading the skill files below is fine).

## Use the migration skills (read these first)

This conversion is the dogfood test of two skills. **Read both in full before touching code**, and follow them as the authoritative migration reference:

- `/Users/jeremymiller/code/ai-skills/skills/marten-migration-v8-to-v9/SKILL.md`
- `/Users/jeremymiller/code/ai-skills/skills/wolverine-migration-v5-to-v6/SKILL.md`

If a skill and the actual RC behavior disagree, the **RC behavior wins** — and that disagreement is a gap to report (see Deliverable 2).

## The RC pin matrix (authoritative)

Bump every critter-stack `PackageReference` to exactly these. CritterStackSamples uses **per-csproj pins** (no `Directory.Packages.props`) — edit each `.csproj` directly.

| Package(s) | Pin |
|---|---|
| `WolverineFx` + all `WolverineFx.*` extensions | `6.0.0-rc.2` |
| `Marten` / `Marten.AspNetCore` / `Marten.Newtonsoft` | `9.0.0-rc.3` |
| `Polecat` (if any) | `4.0.0-rc.2` |
| `JasperFx` / `JasperFx.Events` / `JasperFx.Events.SourceGenerator` / `JasperFx.SourceGeneration` (if referenced directly) | `2.0.0-rc.3` |
| `JasperFx.RuntimeCompiler` (if referenced — 5.x line only, never 2.0.x) | `5.0.0-rc.3` |
| `Weasel.*` (if referenced directly) | `9.0.0-rc.1` |

Most projects only reference `WolverineFx.*` + `Marten*` directly; the JasperFx/Weasel packages usually come transitively — only pin them if a csproj references them explicitly.

## TFM bump (hard requirement)

Wolverine 6 + Marten 9 require **.NET 9+**, and the maintainer has authorized upgrading to .NET 10 freely. **Bump every project to `net10.0`** (this repo currently has a mix of `net8.0`/`net9.0`/`net10.0`). A `net8.0` project will not restore against the RC matrix; standardizing on `net10.0` is the directive. If a specific project has a documented reason to stay lower, note it in the gap report rather than silently downgrading.

## Known migration-relevant code (exercise the skills here)

These files use APIs the skills cover — they're the substance test, not just pin bumps:

- `MartenWithProjectAspire/TripBuildingService/TripProjection.cs` — likely inline-lambda projection registration (`ProjectEvent<>`/`CreateEvent<>`/`DeleteEvent<>`) → apply the Marten skill's "inline-lambda projection registration removed" section: convert to convention methods on a `partial` class + add `Marten.SourceGenerator` analyzer ref + `[assembly: JasperFx.JasperFxAssembly]`.
- `BankAccountES/TransactionHistory.cs` — projection/aggregation; check for inline lambdas + the `UseIdentityMapForAggregates` self-mutation hazard.
- `ProjectManagement/ProjectManagement.Api/Program.cs` — Wolverine bootstrap; check `ServiceLocationPolicy` fallout, `IForwardsTo`, `EventForwardingToWolverine()`, Newtonsoft usage.
- `ProjectManagement/Tests/IntegrationContext.cs` — test host setup; check `WolverineHost.For(...)` → `ForAsync(...)`.

Also globally watch for (per the skills): `ServiceLocationPolicy.NotAllowed` startup throws (`InvalidServiceLocationException`), `UseNewtonsoftForSerialization` → install `WolverineFx.Newtonsoft` + `using`, `SnapshotLifecycle`/`OperationRole` namespace moves, removed Marten codegen knobs + `Internal/Generated/` deletion (Marten side), `RestoreV5Defaults()`/`RestoreV8Defaults()` as the soft-landing escape hatches.

## Approach — pilot first, then sweep

The 13 solutions are independent (separate `.sln`, no shared package management). Don't big-bang all 33 projects.

### Phase 1 — Pilot (commit per solution)
Convert these three first; they cover the spread:
1. **`CqrsMinimalApi`** or another small Marten+Wolverine solution — validates the mechanical pin+TFM path end to end.
2. **`MartenWithProjectAspire`** — exercises the Marten inline-lambda → convention-method projection migration (`TripProjection.cs`).
3. **`ProjectManagement`** — exercises the Wolverine bootstrap migration (`ServiceLocationPolicy`, test-host `ForAsync`).

After each pilot solution: `dotnet build <solution>.sln -c Release`; run its tests if present. **Stop and write the gap report (Deliverable 2) after the pilots** — before sweeping, so skill gaps get captured while fresh.

### Phase 2 — Sweep the remaining solutions
Apply the same recipe to the other 10 (BankAccountES, BookingMonolith, CleanArchitectureTodos, ContributorApi, EcommerceMicroservices, EcommerceModularMonolith, MeetingGroupMonolith, MoreSpeakers, OutboxDemo, PaymentsMonolith). Commit per solution.

If a solution needs a change the skills don't cover, **don't invent a workaround silently** — apply the minimal correct fix, mark it in the gap report, and keep going.

## Deliverables

**1. The conversion** — every solution on the RC matrix, `net9.0`+, building clean. Per-solution commit on `chore/rc-conversion`.

**2. Skill-gap report** (the dogfood payoff) — a markdown table of every code change you had to make that the migration skills did NOT describe, classified:
   - **Skill gap** — a real migration step missing from the skill → file/extend against `JasperFx/ai-skills` (or note for the user to)
   - **Guide gap** — the upstream migration guide (`marten/docs/migration-guide.md` or `wolverine/docs/guide/migration.md`) is missing/wrong → note for an upstream doc fix
   - **RC defect** — the RC genuinely misbehaves (not just undocumented) → candidate issue against the product repo
   - **Sample-only** — quirk specific to this sample, not generalizable

**3. Per-solution status table** — solution | old pins | result (clean / clean-with-changes / blocked) | notes.

## Acceptance

- [ ] All 13 solutions pinned to the RC matrix (per-csproj)
- [ ] No `net8.0` projects remain
- [ ] `dotnet build` clean per solution on `-c Release`
- [ ] Tests pass where present (note any needing a live Postgres/RabbitMQ that you can't run — don't fake them)
- [ ] Inline-lambda projections converted to convention methods on `partial` classes (+ analyzer ref + `[JasperFxAssembly]`)
- [ ] Skill-gap report delivered (Deliverable 2) — even if empty ("skills covered everything" is a valid, valuable result)
- [ ] Per-solution status table delivered
- [ ] Committed on `chore/rc-conversion`, NOT pushed

## Out of scope

- The HelpDesk samples (multi-major Marten 6→9 / Wolverine 1→6 — separate, harder chip after this validates the skills)
- 3rd-party samples (MinimalExamples, wolverine-ef-demo, etc.)
- Editing the migration skills or upstream guides directly — **report** gaps, don't fix cross-repo from here
- Opening the PR (user does that) or pushing

## Don't

- Don't pin `JasperFx.RuntimeCompiler 2.0.x` — 5.x line only (`5.0.0-rc.3`)
- Don't leave any `net8.0` project — it won't restore
- Don't invent undocumented workarounds silently — apply the minimal fix and log it in the gap report
- Don't skip the gap report — it's the primary value of this exercise
- Don't `git push` or open a PR
- Don't edit files outside the CritterStackSamples repo (reading the two skill files is fine)
