# Fleet.SqlServerQueues — CritterWatch on Polecat / SQL Server, no broker

A standalone **CritterWatch** monitoring console (Polecat / SQL Server flavor) watching a small **fleet**
of Wolverine services — with **no message broker at all**. The transport is the database: a single
**SQL Server 2025** is simultaneously the **Polecat event store** *and* the **Wolverine database-backed
queue** that carries the CritterWatch control channel. Everything is provisioned by a .NET Aspire AppHost:
clone, open `Fleet.SqlServerQueues.sln`, press **F5**.

This is the **standalone-Polecat storage showcase** and the SQL Server DB-queue counterpart of the
RabbitMQ flagship. It carries the **Incidents group** alongside the Trip trio (per the locked decision —
DB-queue fleets fully populate the DLQ + Scheduled panels because their durable store *is* the database,
so they're the richest fleets).

## What's in the box

| Project | Role |
|---------|------|
| `AppHost` | .NET Aspire host. Provisions **one** SQL Server 2025 container (event store + queue transport in one — no broker) and launches everything below. The F5 surface. |
| `CritterWatchConsole` | The standalone dashboard. `AddCritterWatch(...)` from **`CritterWatch.SqlServer`** (Polecat) + `UseCritterWatch()`. Owns its own Polecat store on SQL Server; listens for the fleet on the SQL Server DB-queue control channel. |
| `TripMessages` | Shared command/event contracts for the Trip trio. |
| `TripService` | Event-sourced **Polecat** service. Owns the `Trip` aggregate + three async projections. Monitored. |
| `TripPublisher` | Console driver — synthesizes trip traffic, kept flowing via the `ContinueTrip` ping-pong. Monitored. |
| `RepairShop` | Second Polecat service. Handles `RepairRequested` from TripService and replies `RepairsCompleted`. Monitored. |
| `Incidents.Domain` | Shared aggregates / events / commands + the `IncidentsByCategory` multi-stream Polecat projection. |
| `Incidents.Service` | Polecat service — snapshots `Incident` inline, runs `IncidentsByCategory` async (a rebuild-UI target), schedules demo reminders. Monitored. |
| `Incidents.Publisher` | Console driver for incident traffic. Monitored. |
| `Fleet.Common` | One tiny helper: resolves the single SQL Server connection from Aspire config (or a localhost fallback). |
| `Tests` | `Aspire.Hosting.Testing` smoke battery — boots the AppHost and asserts the console is up and the whole fleet (incl. Incidents) registers. |

## The SQL Server DB-queue control channel (the wiring to copy)

There is **no broker**. Every monitored service reports to the console over **Wolverine database-backed
queues** — queue *tables* in the same SQL Server. The convention:

**Console side** (`CritterWatchConsole/Program.cs`):

```csharp
// AddCritterWatch comes from the CritterWatch.SqlServer package (Polecat). It registers the console's
// Polecat ancillary store + its own "critterwatch_wolverine" durability store with the #531
// ResolveMainStoreOnConflict reconciliation already wired.
builder.AddCritterWatch(sqlServerConnString, configureWolverine: opts =>
{
    // Stand up the SQL Server DB-queue transport on the SAME SQL Server (no schema arg → "dbo").
    opts.UseSqlServerPersistenceAndTransport(sqlServerConnString).AutoProvision();

    opts.ListenToSqlServerQueue("critterwatch")   // THE shared control/telemetry queue (a queue TABLE)
        .ListenOnlyAtLeader()                      // one console node owns it (no split-brain)
        .UseCritterWatchSerializer();              // pin CritterWatch's wire-format
});
```

**Monitored side** (every service's `Program.cs`):

```csharp
// role: Ancillary keeps the service's Polecat event store as the Main durability store.
opts.UseSqlServerPersistenceAndTransport(sqlServerConnString, role: MessageStoreRole.Ancillary).AutoProvision();
opts.ListenToSqlServerQueue("trip_service");   // this service's own inbound control queue

opts.AddCritterWatchMonitoring(
    "sqlserver://critterwatch".ToUri(),    // -> the console's shared queue (telemetry/registration)
    "sqlserver://trip_service".ToUri());   // -> this service's queue (commands route back here)
```

So: the **first** `AddCritterWatchMonitoring` URI always points at `sqlserver://critterwatch` (the
console's queue), and the **second** is the service's own queue, matching its `ListenToSqlServerQueue(...)`.
Each service uses a distinct second queue: `trip_service`, `trip_publisher`, `repair_shop`,
`incident_service`, `incident_publisher`.

### ⚠️ The #1 failure mode — share ONE transport schema for the control queue

A broker queue is addressed by name across the network; a **DB-backed queue is a table in a specific
schema**. `sqlserver://critterwatch` resolves to a queue *table* named `critterwatch` **in that node's
Wolverine transport schema**. So for the console to actually receive a service's telemetry, **console +
every monitored service must use the SAME Wolverine transport schema** for that control queue — otherwise
each writes to its own private `critterwatch` table and the console never sees it.

- **Do:** let console + all services use the **same** transport schema (here: the default `dbo` — we pass
  **no** `transportSchema`). Each service still keeps its OWN distinct **Polecat event-store** schema
  (`trips` / `repair_shop` / `incidents`) — only the transport/queue schema must coincide.
- **Don't:** set a per-service `transportSchema` — it isolates the queue table and silently breaks
  delivery to the console.

`Tests/FleetSmokeTests.cs` asserts all services (incl. `IncidentService`) appear in `/services` — which is
exactly the assertion that catches a per-service-transport-schema mistake (a single-host control-channel
test wouldn't).

## Polecat specifics vs. the Marten flagship

- Console uses **`CritterWatch.SqlServer`** (`AddCritterWatch(sqlServerConnString, …)`), not `CritterWatch`.
- Services are **Polecat-backed**: `opts.Services.AddPolecat(...)`, projections from
  `Polecat.Projections`, `PolecatOps.StartStream<T>` instead of `MartenOps.StartStream<T>`.
- **Explicit `ApplyAllDatabaseChangesOnStartup()`** on the Polecat integration: Polecat doesn't yet
  register an `IStatefulResource` the way Marten does (JasperFx/polecat#187), so
  `AddResourceSetupOnStartup()` alone never creates the Polecat event tables. Calling the schema applier
  explicitly provisions them until the upstream `SystemPart` lands.
- **No conventional routing** — the SQL Server transport has no convention-based routing (RabbitMQ's
  `UseConventionalRouting()`), so the publishers route each command type explicitly with
  `PublishMessage<T>().ToSqlServerQueue(...)` and the services listen on a dedicated command queue.

## Why SQL Server 2025?

Polecat stores event payloads in the native `json` column type, which only exists in **SQL Server 2025**
(v17). Older engines (incl. Azure SQL Edge) fail Polecat schema creation with
`SqlException 2715 "Cannot find data type json"`. The AppHost pins the **`2025-latest`** image tag
(`mcr.microsoft.com/mssql/server:2025-latest`) for exactly this reason.

## Connection resolution

`Fleet.Common/SampleConnections.cs` reads `ConnectionStrings__critterstore` (injected by Aspire's
`.WithReference(db)`), falling back to the localhost docker-compose SQL Server (host port **1443**, SA
password `P@55w0rd`) so any project still runs standalone.

## Running

- **F5 / Aspire (recommended):** open `Fleet.SqlServerQueues.sln`, run `AppHost`. Aspire pulls the SQL
  Server 2025 image, starts it, then launches the console + the fleet. Open the `critterwatch` resource's
  endpoint to see the dashboard; the fleet appears under Services after each service's first heartbeat.
  > First boot of the 2025 image can be slow on Apple Silicon (amd64 emulation + the msdb upgrade refuses
  > connections for ~30–60 s) — the harness allows up to 5 minutes for the resource to reach Running.
- **Standalone:** `docker compose up -d` a SQL Server 2025 on `1443`, then `dotnet run` any project. The
  localhost fallback in `SampleConnections` takes over.

## Tests

```bash
dotnet test                                       # needs a running Docker daemon (Aspire starts the container)
dotnet test --filter "Category!=DockerRequired"   # skip the container battery
```

`Tests/FleetSmokeTests.cs` boots the AppHost once, asserts `GET /api/critterwatch/about` → 200, then polls
`GET /api/critterwatch/services` until `TripService`, `RepairShop`, and `IncidentService` have registered.

## Packages

Consumes the **published** CritterWatch NuGets (`CritterWatch.SqlServer`, `Wolverine.CritterWatch`) — never
a project reference into the CritterWatch repo. The SQL Server DB-queue control channel depends on the
local `WolverineFx.SqlServer` **6.14.1-cw3252** pin (the #531 / `wolverine#3248` fix). Versions are
centrally pinned in `../Directory.Packages.props` (Central Package Management is on for this island),
resolved from the local feed via the gitignored `../nuget.config` until CritterWatch 1.0 publishes to
nuget.org.

## Notes / deviations

- **No chaos-monkey / partitioning machinery** — kept minimal per the plan's "keep it minimal" gotcha.
- **No OpenTelemetry exporter wiring** — OTel tracing is deferred from the initial battery.
- **No no-op Rewind subscription.** The Marten flagship's TripService carries a `SubscriptionBase` no-op
  shard to light up the Rewind action; that's omitted here to stay on the proven Polecat projection
  surface. The three async projections still give the Projections / pause / restart / rebuild UI live
  targets.
- **`ContinueTrip` is driven from the command handlers** (via `OutgoingMessages`), not from a projection
  `RaiseSideEffects` override — matching the upstream PolecatTrips sample and avoiding the Marten-specific
  `IEventSlice` side-effect API.
- Doc-embeddable regions are marked with `// begin-snippet: <name>` / `// end-snippet`.
```
