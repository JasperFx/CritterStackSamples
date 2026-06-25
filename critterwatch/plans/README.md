# CritterWatch samples — agent handoff plans

Each file in this folder is a **self-contained task for one dedicated agent**. Read this
README first (shared context + conventions), then your assigned plan file. Work happens on
the branch **`feat/critterwatch-samples`** in `~/code/CritterStackSamples`.

## What this program is

Build F5-able sample solutions for **CritterWatch** (the Critter Stack monitoring console).
Each solution: clone → open `.sln` → press **F5**; the Aspire AppHost provisions every Docker
dependency. Each ships an `Aspire.Hosting.Testing` battery that asserts the app launches and
the CritterWatch HTTP routes respond.

## Locked decisions (do not relitigate)

- **Package refs:** samples consume **published CritterWatch NuGets**. The *expected next release*
  is staged in a local feed (`~/code/_cw_localfeed`) as **`1.0.0-alpha.4`**; the gitignored
  `critterwatch/nuget.config` already resolves it. Samples use the packed nugets, **never** a
  project reference into `~/code/CritterWatch`.
- **Storage coverage rides the transport axis** — don't build a full Marten×Polecat grid. Most
  fleets are Marten/Postgres; `SqlServerQueues` is Polecat/SQL Server; both embedded variants exist.
- **OTel tracing is deferred** out of the initial battery (fast-follow, pull-model via `ITraceProvider`).
- **Incidents group only in:** `Fleet.RabbitMq` (flagship) + `Fleet.PostgresqlQueues` + `Fleet.SqlServerQueues`.
  All other fleets run only the Trip trio.

## Current state (Phase 0 — DONE)

- Branch `feat/critterwatch-samples` exists.
- Local feed packed at `1.0.0-alpha.4`: `CritterWatch`, `CritterWatch.SqlServer`,
  `CritterWatch.Abstractions`, `Wolverine.CritterWatch`, `Wolverine.CritterWatch.Http`.
  (⚠️ `CritterWatch.Embedded` failed to pack — NU5026, csproj missing the empty-`<TargetFramework>`
  multi-target trick. The embedded *extensions* live in the `CritterWatch` package anyway; the
  `Embedded.*` plan covers fixing/working around this.)
- Config island at `critterwatch/`: `Directory.Build.props` (net10.0, CPM on),
  `Directory.Packages.props` (pins aligned to CritterWatch's real closure),
  `nuget.config` (gitignored, packageSourceMapping), `README.md`. Restore is validated.

### CritterWatch-side changes MUST go in the dedicated worktree

Do **not** modify the main `~/code/CritterWatch` working copy (it carries unrelated uncommitted work).
All samples-driven CritterWatch changes (csproj fixes, control-channel/upstream work, re-packing the
expected release) happen in the dedicated worktree:

- **Worktree:** `~/code/_cw_worktrees/samples-support` — branch `samples/cw-support` (off `main`).
- It already has CritterWatch's gitignored `nuget.config` copied in (required to restore the `-cw*`
  Wolverine pins; a fresh worktree doesn't inherit gitignored files). If you add another worktree, copy
  `nuget.config` into it first.

To re-pack the feed (release-cycle step), from the worktree:
```bash
cd ~/code/_cw_worktrees/samples-support
dotnet pack <project.csproj> -c Release -p:Version=1.0.0-alpha.4 -p:PackageVersion=1.0.0-alpha.4 \
  --output ~/code/_cw_localfeed -p:ContinuousIntegrationBuild=true
```
Reading CritterWatch source for reference is fine from either location; only *writes* require the worktree.

## Repo conventions

- **One `.sln` per solution**, in its own folder under `critterwatch/` (e.g. `critterwatch/Fleet.RabbitMq/`).
- net10.0; CPM is on — **never put a `Version=` on a `PackageReference`**; add the pin to
  `critterwatch/Directory.Packages.props` instead (shared across all solutions).
- Each solution folder: `AppHost/` (Aspire), the console + service projects, and `Tests/`.
- Aspire AppHost csproj uses `<Sdk Name="Aspire.AppHost.Sdk" Version="9.5.0"/>`.
- **Heavily annotate** sample code — these are teaching artifacts. Mark doc-embeddable regions with
  `// begin-snippet: <name>` / `// end-snippet` (see `08-docs-snippet-scrape.md`).

## Source material (sibling repo `~/code/CritterWatch/src/Samples`)

Port — don't reinvent. Restructure to consume packed nugets instead of project refs.

| Need | Source |
|------|--------|
| Standalone console host (consumes packed nuget) | `src/Smoke/**/Program.cs` |
| Trip trio + messages (Rabbit/Marten) | `src/Samples/Trips/{TripMessages,TripService,TripPublisher,RepairShop}` |
| Incidents group | `src/Samples/Incidents/{Incidents.Domain,Incidents.Service,Incidents.Publisher}` |
| ASB emulator wiring | `src/Samples/Trips2` (+ `Trip2AsbConfig`) |
| Amazon SQS / LocalStack | `src/Samples/Trips3` |
| Polecat / SQL Server fleet | `src/Samples/PolecatTrips` |
| Embedded self-monitoring | `src/Samples/EmbeddedDemo/{EmbeddedDemo.Marten,EmbeddedDemo.Polecat}` |
| Modular monolith + ancillary store | `src/Samples/ModularMonolith`, `src/Samples/MultiStoreHost` |
| Control-channel composition + Aspire patterns | `src/BffHost/Program.cs`, `src/BffHost/Composition/**` |

## Key API surface (from `~/code/CritterWatch`)

- **Standalone Marten console:** `builder.AddCritterWatch(connString, configureWolverine, configureHub, enableClusterPartitioning, configureClusterShardedTopology, schemaName)` then `app.UseCritterWatch()`. Defined in `src/CritterWatch.Services/Hosting/CritterWatchHostingExtensions.cs`.
- **Standalone Polecat console:** `opts.AddCritterWatchServices(sqlServerConnString, ...)` (Polecat flavor) — `src/CritterWatch.Services.SqlServer/WolverineOptionsExtensions.Polecat.cs`.
- **Embedded:** `opts.AddCritterWatchEmbedded(connString, hostOwnsPrimaryStore, schemaName)` + `app.MapCritterWatchEmbedded()` — `src/CritterWatch.Services/Hosting/CritterWatchEmbeddedExtensions.cs`. `hostOwnsPrimaryStore: true` attaches CritterWatch's ancillary store to a store the host already owns.
- **Monitored side:** `opts.AddCritterWatchMonitoring(critterWatchUri, controlUri)` with optional `.MetricsDataSource("prometheus")` / `.TraceProvider(...)`. Options in `Wolverine.CritterWatch/CritterWatchOptions.cs`.
- The console's storage is always its own (Postgres for Marten flavor / SQL Server for Polecat). Monitored services reach it over the chosen transport's **control channel**.

## Definition of done (every solution)

1. `dotnet build` clean against the local feed.
2. `F5` on the AppHost brings up all containers + projects; CritterWatch dashboard reachable and
   shows the fleet services registered.
3. `Tests/` battery green: AppHost launches via `Aspire.Hosting.Testing`, and
   `GET /api/critterwatch/about` + `GET /api/critterwatch/services` return 200 with the expected
   fleet members present.
4. Sample code annotated; doc-embeddable regions marked.
5. A short per-solution `README.md`.

## Plan files

| File | Dedicated agent task |
|------|----------------------|
| `01-fleet-rabbitmq-flagship.md` | Flagship — **defines the template** every other solution copies. Build first. |
| `02-embedded.md` | `Embedded.Marten` + `Embedded.Polecat` (modular monolith, ancillary store, self-monitoring) |
| `03-fleet-other-transports.md` | `Fleet.AmazonSqs`, `.AzureServiceBus`, `.Kafka`, `.Redis`, `.GooglePubSub` |
| `04-fleet-db-queues.md` | `Fleet.PostgresqlQueues` (Marten) + `Fleet.SqlServerQueues` (Polecat); both carry Incidents |
| `05-webservice-http.md` | `WebService.Http` — monitor a Wolverine HTTP service |
| `06-metrics-prometheus.md` | `Metrics.Prometheus` — services in meter-only mode, CritterWatch polls Prometheus |
| `07-shared-test-harness.md` | Shared `Aspire.Hosting.Testing` helper + route-smoke assertions |
| `08-docs-snippet-scrape.md` | mdsnippets-based scraper from sample code into CritterWatch docs |
| `09-upstream-control-channel-spike.md` | Read-only spike: control-channel parity on Kafka/Redis/GCP/DB queues (gates 03/04) |

**Build order:** `07` (harness) → `01` (flagship) → `09` (spike, parallel) → `02`, `05`, `06`
(no upstream blockers) → `03`, `04` (after the spike) → `08` (after a few solutions exist).
