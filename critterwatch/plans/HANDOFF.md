# HANDOFF — CritterWatch samples (read this first)

You're picking up the CritterWatch sample-solutions effort. This doc is self-contained:
current state, what's committed, the two live bugs with diagnoses, the tooling gotchas
that will bite you, and the exact next steps. Then read `plans/README.md` for the program
conventions and `plans/01-fleet-rabbitmq-flagship.md` for the verified template.

## Where the code lives

- **Samples:** `~/code/CritterStackSamples`, branch **`feat/critterwatch-samples`** (head `bdf7238`).
  All CritterWatch samples are under `critterwatch/`.
- **CritterWatch source changes go in a WORKTREE, never the main checkout:**
  `~/code/_cw_worktrees/samples-support`, branch **`samples/cw-support`** (head `c0e9f58f`),
  off `~/code/CritterWatch` `main`. The main checkout has unrelated uncommitted work — do not touch it.
  The worktree already has CritterWatch's gitignored `nuget.config` copied in (needed to restore the
  `-cw*` Wolverine pins).
- **Local NuGet feed:** `~/code/_cw_localfeed`. The samples consume the CritterWatch **`1.0.0-alpha.4`**
  packages from here via the gitignored `critterwatch/nuget.config` (packageSourceMapping). CritterWatch
  is not yet on nuget.org at this version.

## Current board

| Sample | State |
|--------|-------|
| `Fleet.RabbitMq` | ✅ GREEN (battery passes) — the flagship/template |
| `Fleet.AzureServiceBus` | ✅ GREEN (ASB emulator) |
| `Fleet.PostgresqlQueues` | ✅ GREEN (Postgres DB-queue + Incidents) |
| `Fleet.SqlServerQueues` | ❌ 1/2 — console up, services don't register (BUG A below) |
| `WebService.Http` | ❌ services don't register (BUG B below) |
| `Fleet.AmazonSqs` | ↩️ pulled this round — [CritterStackSamples#5](https://github.com/JasperFx/CritterStackSamples/issues/5) (Aspire/LocalStack startup) |
| `Fleet.GooglePubSub` | ⏭️ deferred — [wolverine#3258](https://github.com/JasperFx/wolverine/issues/3258) (leader-pinned per-node subscriptions) |
| `Fleet.Redis`, `Fleet.Kafka` | ⬜ not built (Redis ✅-ready per spike; Kafka ⚠️ single-partition control topic) |
| `Embedded.Marten`, `Embedded.Polecat` | ⬜ not built (see `plans/02-embedded.md`) |
| `Metrics.Prometheus` | ⬜ not built (server-side already exists; see `plans/06`) |

All three RED/❌ failures share the same symptom: console `/about` → 200, but
`GET /api/critterwatch/services` stays empty (`Registered: [(none)]`). See
[CritterStackSamples#6](https://github.com/JasperFx/CritterStackSamples/issues/6).

## What was just shipped (DONE — don't redo): the serializer fix

`.UseCritterWatchSerializer()` is now **100% unnecessary on every transport**. The brotli serializer
used content-type `application/json` (== host default), so it could only be pinned per-endpoint via a
fragile name-based policy that missed DB-queue/HTTP listeners. Fix (worktree commit `c0e9f58f`):

- `BrotliJsonMessageSerializer.ContentType` → unique **`binary/critterwatch`** (`CritterWatchContentType` const).
- `opts.AddSerializer(...)` registers it **globally** in both `AddCritterWatchMonitoring`
  (`Wolverine.CritterWatch/WolverineOptionsExtensions.cs`) and `AddCritterWatch`
  (`src/CritterWatch.Services/WolverineOptionsExtensions.cs`). Wolverine resolves it by content-type on
  any endpoint via the global-registry fallback (`Endpoint.TryFindSerializer` → `Runtime.Options`).
  Unique content-type → never shadows the host's `application/json` (embedded-safe).

Validated: `Fleet.PostgresqlQueues` went red→green with zero serializer config; brokers stay green with
their `.UseCritterWatchSerializer()` calls removed (samples commit `bdf7238`). **This proved the
serializer was PG's only blocker — but NOT SQL's or HTTP's** (they stayed red after the fix), so:

## The two live bugs (your job)

### BUG A — `Fleet.SqlServerQueues`: services don't register
- **Ruled out:** the serializer (global fix didn't recover it); `.ListenOnlyAtLeader()` on the console
  (removing it didn't help — reverted).
- **Best-guess root cause:** the SQL console wires the transport DIFFERENTLY from the green PG console.
  PG console just does `ListenToPostgresqlQueue("critterwatch")` and relies on AddCritterWatch's **Marten**
  `IntegrateWithWolverine` to enable the Postgres transport. The SQL console additionally calls
  `UseSqlServerPersistenceAndTransport(conn)` explicitly — a **dual store registration** alongside
  AddCritterWatch's Polecat integration (which puts its store in `critterwatch_wolverine`; #531 demotes it
  to ancillary). Suspect the console's `critterwatch` control queue ends up in a different store/schema than
  where the services publish `sqlserver://critterwatch` — so telemetry is delivered to a queue nobody reads.
  Possibly **Polecat's `IntegrateWithWolverine` doesn't auto-enable the SQL Server transport the way
  Marten's does**, which is why the agent added the explicit call. If so, the real fix is a CritterWatch.SqlServer
  change (in the worktree) to make `AddCritterWatch` (Polecat flavor) enable the SQL transport so the sample
  can drop the explicit `UseSqlServerPersistenceAndTransport` and mirror PG.
- **Files:** `critterwatch/Fleet.SqlServerQueues/CritterWatchConsole/Program.cs` (console) +
  `.../TripService/Program.cs` (a service). Compare against the green `Fleet.PostgresqlQueues/*`.

### BUG B — `WebService.Http`: services don't register
- **Best-guess root cause:** the service→console telemetry POST over Wolverine's HTTP transport isn't
  landing. The service `AddCritterWatchMonitoring(telemetryUri = {consoleBaseUrl}/_wolverine/invoke, ...)`
  and registers a named `HttpClient` (name == the telemetry URI, `BaseAddress` == it) +
  `IWolverineHttpTransportClient`. Suspect `consoleBaseUrl` (from Aspire service discovery,
  `Fleet.Common.SampleConnections.ConsoleBaseUrl()`) isn't resolving to the console's reachable URL at
  runtime, so the POST never reaches the console. The console maps `MapWolverineHttpTransportEndpoints()`
  and sets the CritterWatch serializer as its default — receive side looks correct.
- **Files:** `critterwatch/WebService.Http/OrderService/Program.cs`,
  `.../CritterWatchConsole/Program.cs`, `.../Fleet.Common/SampleConnections.cs`.

## ⚠️ PREREQUISITE before diagnosing either: add resource-log capture to the harness

You currently CANNOT see the console/service logs, which is what both bugs need. The blockers:
- Aspire AppHost `dotnet run` stdout shows only its own dashboard line — **child resource logs go to the
  dashboard, not stdout**. And `dotnet run` on an AppHost aborts (exit 134) unless you set
  `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`, `ASPNETCORE_URLS=...`, `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL=...`.
- Killing a manually-run AppHost orphans its Aspire containers (`docker rm -f` the stray `sqlserver-*` etc.).

**Do this first:** extend `critterwatch/Shared/CritterWatch.Samples.Testing/` to capture resource logs and
dump them when `WaitForServicesAsync` times out. In the fixture, after `app.StartAsync()`, use
`app.Services.GetRequiredService<ResourceLoggerService>().WatchAsync("<resource>")` (or
`ResourceNotificationService`) to stream each resource's logs into a buffer, and on assertion failure write
the `critterwatch` + service logs to test output. Then the SQL/HTTP failures will tell you exactly where
telemetry dies (sent? delivered? wrong schema/queue? deserialize error? POST 404?).

## How to run & iterate (tooling cheat-sheet)

- **Run one sample's battery (needs Docker):** `cd critterwatch/<Sample> && dotnet test Tests/Tests.csproj -c Debug`.
  Check the tail for `Passed!`/`Failed!` and `Missing:`/`Registered:`. Run batteries **serially** — concurrent
  Aspire stacks contend for Docker and cause false failures. The CritterWatch docker-compose dev stack is
  often up too (fixed-port clashes possible for fixed-port samples).
- **macOS has no `timeout`** — don't use it.
- **After a CritterWatch source change (worktree):** re-pack to the feed, then **clear the NuGet cache**
  (same version won't re-extract otherwise):
  ```bash
  cd ~/code/_cw_worktrees/samples-support
  dotnet pack <proj> -c Release -p:Version=1.0.0-alpha.4 -p:PackageVersion=1.0.0-alpha.4 --output ~/code/_cw_localfeed
  rm -rf ~/.nuget/packages/critterwatch ~/.nuget/packages/critterwatch.sqlserver ~/.nuget/packages/wolverine.critterwatch
  ```
  The packable projects: `Wolverine.CritterWatch` (+`.Http`), `CritterWatch.Abstractions`,
  `src/CritterWatch.Services` (→ `CritterWatch`), `src/CritterWatch.Services.SqlServer` (→ `CritterWatch.SqlServer`).
  (`CritterWatch.Embedded` fails to pack — NU5026, csproj missing the empty-`<TargetFramework>` multi-target
  trick; only matters for the embedded samples, and `AddCritterWatchEmbedded` actually lives in the
  `CritterWatch` package anyway.)
- **CPM is on** in `critterwatch/` — never put `Version=` on a `PackageReference`; add pins to
  `critterwatch/Directory.Packages.props`.

## Suggested order

1. Harness resource-log capture (above) — unblocks everything.
2. BUG A (SQL Server) — likely a CritterWatch.SqlServer worktree fix (Polecat transport enabling) + sample simplification.
3. BUG B (HTTP) — likely a sample-wiring fix (console URL resolution); confirm with logs.
4. Then the unbuilt samples: `Fleet.Redis`, `Fleet.Kafka` (`plans/03`), `Embedded.Marten`/`.Polecat` (`plans/02`),
   `Metrics.Prometheus` (`plans/06`).
5. `Fleet.AmazonSqs` (#5) and `Fleet.GooglePubSub` (wolverine#3258) when their blockers clear.
6. Docs snippet scraper (`plans/08`) once enough samples are green.

## Don't break
- The 3 green samples (Rabbit, ASB, Postgres-queue) — re-run their batteries after any CritterWatch
  re-pack to confirm no regression.
- The serializer fix (`c0e9f58f`) — it's the foundation that makes the consoles config-free.
