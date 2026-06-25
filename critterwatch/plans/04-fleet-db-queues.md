# 04 — `Fleet.PostgresqlQueues` + `Fleet.SqlServerQueues`

**Read `plans/README.md` + `01-fleet-rabbitmq-flagship.md` first.** These two double as the
**standalone-Marten** and **standalone-Polecat** storage showcases, and both **carry the Incidents
group** (per the locked decision) since they're the "full fleet" exemplars.

| Solution | Transport | Storage | Console pkg | Incidents? |
|----------|-----------|---------|-------------|------------|
| `Fleet.PostgresqlQueues` | Wolverine PostgreSQL DB-backed queues (`WolverineFx.Postgresql`, `ListenToPostgresqlQueue`) | Marten/Postgres | `CritterWatch` | yes |
| `Fleet.SqlServerQueues` | Wolverine SQL Server DB-backed queues (`WolverineFx.SqlServer`) | Polecat/SQL Server 2025 | `CritterWatch.SqlServer` | yes |

> **UNBLOCKED** — spike `09-FINDINGS.md` confirms DB-queue control channel **works as-is** (#531 /
> `wolverine#3248`, shipped in the `-cw3252` pins; cross-engine repro green at
> `src/Tests/Integration/db_queue_control_channel_cross_engine.cs`). CritterWatch pins DB queues to
> `BufferedInMemory` and reconciles the Main-store conflict. **DLQ + Scheduled panels fully populate**
> (these read the durable store, which here IS the DB) — this is the richest fleet, hence Incidents lives
> here. Note it depends on the local `-cw3252` pins, not nuget.org.

## CRITICAL design note — DB-queue control channel is schema-coupled (learned the hard way)

Unlike a broker, a Wolverine DB-backed queue is a **table in a specific schema**. `AddCritterWatchMonitoring`
routes to `postgresql://critterwatch` / `sqlserver://critterwatch` via `Publish(...).To(uri)` where the URI
host (`critterwatch`) is the queue name, resolved to a queue **table in that node's transport schema**. So for
a multi-host fleet to actually share the one `critterwatch` control queue, **every participant (console +
all monitored services) must use the SAME Wolverine transport schema** — otherwise each service writes to its
own `critterwatch` table in its own schema and the console never sees the telemetry.

- **Do:** let console + all services use the **same** transport/message-storage schema for the control queue
  (the default, or one explicit shared value passed identically everywhere). Each service still keeps its OWN
  distinct Marten/Polecat **event-store** schema — only the transport/queue schema must coincide.
- **Don't:** copy the DocSamples `transportSchema: "myapp_cw_control"` per-service — a per-service transport
  schema isolates the queue table and silently breaks delivery to the console.
- Verify in the battery that services actually appear in `/services` (this is the failure mode that wouldn't
  show up in a single-host test — the spike's cross-engine tests were single-host).

## Build notes
- **No broker container** — the transport IS the database. AppHost provisions only Postgres (resp. SQL Server)
  and uses the same DB for both event storage and the Wolverine queue tables. This is the simplest AppHost
  of the set; lean into that in the annotation ("one container, no broker").
- Console: the `Postgres` variant mirrors `src/Smoke` exactly (`opts.ListenToPostgresqlQueue("critterwatch")`).
  The `SqlServer` variant uses the Polecat flavor `AddCritterWatchServices(sqlServerConnString, ...)` —
  read `src/CritterWatch.Services.SqlServer/WolverineOptionsExtensions.Polecat.cs`.
- For `SqlServerQueues`, port the Polecat fleet from `src/Samples/PolecatTrips`; reuse the schema-isolation
  notes there (`MessageStorageSchemaName`, manual `ApplyAllDatabaseChangesOnStartup`).
- SQL Server image: pin a **SQL Server 2025** tag in `AddSqlServer(...)` (Polecat requires 2025+).

## Tests / DoD
Per `plans/README.md`, including the Incidents.Service registration + an async-projection rebuild smoke
if cheap (the Incidents `IncidentsByCategory` projection is async specifically to exercise rebuilds).
