# 08 — Docs snippet scraper (samples → CritterWatch docs)

**Read `plans/README.md` first. Do this after a few solutions exist** (at least the flagship), so there
are real annotated regions to pull.

Goal: CritterWatch's Installation/docs pages embed **annotated snippets pulled from the sample code**,
with manual control over which snippet goes where, but **automated, drift-gated extraction** — no
hand-copying that silently drifts.

## Approach (reuse mdsnippets — already in CritterWatch)
CritterWatch already pins `markdownsnippets.tool` and has a `VerifyDocs` Nuke gate
(`~/code/CritterWatch` — see CLAUDE.md "Documentation code samples (mdsnippets)"). Extend it to scan the
samples repo as an additional source root rather than writing a bespoke parser.

1. **Mark regions** in the sample code with mdsnippets markers:
   `// begin-snippet: critterwatch-fleet-rabbitmq-console` … `// end-snippet`.
   (Agents building `01`–`06` add these as they go — call it out in their PRs.)
2. **Scraper script** (`scripts/scrape-sample-snippets.sh` in CritterWatch, or a Nuke target): clone/pull
   `CritterStackSamples` at a **pinned tag/commit** into a known path, then run mdsnippets with that path
   added to the scan roots so `<!-- snippet: <name> -->` references in the docs resolve against the samples.
3. **Reference** snippets in the relevant docs pages (Installation, embedded, per-transport) with
   `<!-- snippet: <name> -->` … `<!-- endSnippet -->`.
4. **Drift gate:** wire into `VerifyDocs` — re-scrape then `git diff --exit-code` the markdown; fail on drift.
   Pin the samples commit so docs are reproducible.

## Deliverables
- The scraper script/target + docs config pointing at the pinned samples checkout.
- A couple of real embedded snippets in the CritterWatch Installation docs (console host + a monitored
  service `AddCritterWatchMonitoring` call) proving the pipeline.
- Doc note on how to bump the pinned samples commit.

## Notes
- Verify mdsnippets supports multiple/extra scan roots in the installed `markdownsnippets.tool` version;
  if not, the fallback is a thin script that copies marked regions out of the samples into a generated
  includes folder the docs reference — same marker convention, same drift gate.
