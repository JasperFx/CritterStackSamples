# Fleet.PostgresqlQueues — CritterWatch over Wolverine Postgres DB-backed queues

A standalone **CritterWatch** monitoring console watching a small **fleet** of Wolverine services with
**no message broker at all** — the transport IS the database. Wolverine's **PostgreSQL DB-backed queues**
live as tables in the same **Postgres** that backs every event store, so the .NET Aspire AppHost
provisions exactly **one container**: clone, open `Fleet.PostgresqlQueues.sln`, press **F5**.

This is the Marten/Postgres storage showcase and the "richest" fleet (DLQ + Scheduled panels populate
from the durable store, which here *is* the DB), so it carries the **Incidents group** alongside the Trip
trio — the same shape as the RabbitMQ flagship, transport swapped.

## What's in the box

| Project | Role |
|---------|------|
| `AppHost` | .NET Aspire host. Provisions **one Postgres** container (no broker) and launches everything below. The F5 surface. |
| `CritterWatchConsole` | The standalone monitoring dashboard. `AddCritterWatch(conn, configureWolverine: opts => opts.ListenToPostgresqlQueue("critterwatch"))` + `UseCritterWatch()`. Owns its own Postgres store; listens for the fleet on the Postgres control queue. |
| `TripMessages` | Shared command/event contracts for the Trip trio. |
| `TripService` | Event-sourced Marten service. Owns the `Trip` aggregate + three async projections + a no-op subscription. Monitored. |
| `TripPublisher` | Console driver — synthesizes trip traffic and keeps it flowing via the `ContinueTrip` ping-pong. Monitored. |
| `RepairShop` | Second Marten service. Handles `RepairRequested` from TripService and replies `RepairsCompleted`. Monitored. |
| `Incidents.Domain` | Shared aggregates / events / commands + the `IncidentsByCategory` multi-stream projection. |
| `Incidents.Service` | Marten service — snapshots `Incident` inline, runs `IncidentsByCategory` async (a rebuild-UI target), schedules demo reminders. Monitored. |
| `Incidents.Publisher` | Console driver for incident traffic. Monitored. |
| `Fleet.Common` | One tiny helper: resolves the Postgres connection from Aspire config (or a localhost fallback). |
| `Tests` | `Aspire.Hosting.Testing` smoke battery — boots the AppHost and asserts the console is up and the **whole** fleet (incl. Incidents) registers. |

## The Postgres DB-queue control channel (the wiring to copy)

There is **no broker**. Every monitored service reports to the console over one shared Wolverine
PostgreSQL queue table named `critterwatch`. Two things make this work — and one of them is a silent
failure mode if you get it wrong.

**Console side** (`CritterWatchConsole/Program.cs`) — mirrors CritterWatch's own `src/Smoke` host:

```csharp
builder.AddCritterWatch(
    consoleConnectionString,
    configureWolverine: opts =>
    {
        // The control channel — a broker-free Postgres queue listener. AddCritterWatch reconciles the
        // DB-transport's "Main store" requirement against its own durability store and pins the route to
        // BufferedInMemory automatically (a DB queue can't run Inline).
        opts.ListenToPostgresqlQueue("critterwatch");
    },
    enableClusterPartitioning: false);
```

**Monitored side** (every service's `Program.cs`):

```csharp
// Stand up Wolverine's DB-backed queues IN THE SAME POSTGRES that backs the event store.
//  * role: Ancillary  -> the Marten IntegrateWithWolverine store stays Main; the transport store doesn't
//                        register a competing Main (it would throw at startup otherwise).
//  * transportSchema  -> "critterwatch_wolverine", the schema CritterWatch 1.0 pins the console's queue
//                        tables to. Shared by EVERYONE, so it is the one "critterwatch" queue table.
opts.UsePostgresqlPersistenceAndTransport(connectionString, transportSchema: "critterwatch_wolverine",
    role: MessageStoreRole.Ancillary)
    .AutoProvision();

opts.ListenToPostgresqlQueue("trip_service_control");   // this service's own control queue (2nd URI)

opts.AddCritterWatchMonitoring(
    "postgresql://critterwatch".ToUri(),         // -> the console's shared control/telemetry queue
    "postgresql://trip_service_control".ToUri()); // -> this service's control queue (commands route back)
```

### ⚠️ Schema coordination is the whole game

A Wolverine DB-backed queue is a **table in a specific schema**, not a broker destination. `postgresql://critterwatch`
resolves to a queue table `critterwatch` **in that node's transport schema**. So for the multi-host fleet
to share the one control queue, **the console AND every monitored service must use the SAME Wolverine
transport schema**. Since CritterWatch 1.0 (#1025; tracked as CritterWatch#1126) the console's side of that is fixed: `AddCritterWatch`
pins the console's Wolverine transport *and* durability tables to `{schema}_wolverine` — `critterwatch_wolverine`
for the default `critterwatch` schema — and nothing in `configureWolverine` can override it. So every monitored
service passes `transportSchema: "critterwatch_wolverine"`. Leave a service on Wolverine's default
(`wolverine_queues`), or copy the DocSamples' `transportSchema: "myapp_cw_control"` onto it, and it writes to
its own private `critterwatch` table the console never reads. The `Tests` battery asserts all five services
register precisely to catch that.

Each service still keeps its **own distinct Marten event-store schema** (`trips`, `repair_shop`,
`incidents`, …) — only the transport/queue schema must coincide.

### Cross-app routing is explicit (no conventional routing)

Unlike RabbitMQ, the Wolverine PostgreSQL transport has **no** `UseConventionalRouting()`. Every cross-app
route is wired explicitly with `PublishMessage<T>().ToPostgresqlQueue("…")` against a matching
`ListenToPostgresqlQueue("…")` on the other side. Business traffic and CritterWatch operator-control
traffic use **separate** queues per service (e.g. `trip_commands` for domain messages,
`trip_service_control` for operator commands).

## Connection resolution

`Fleet.Common/SampleConnections.cs` reads `ConnectionStrings__critterstore` (injected by Aspire's
`.WithReference(db)`), falling back to the localhost docker-compose Postgres so any project still runs
standalone. That single connection string backs **both** the event store and the Wolverine queue tables.

## Running

- **F5 / Aspire (recommended):** open `Fleet.PostgresqlQueues.sln`, run `AppHost`. Aspire pulls the single
  Postgres image, starts the console, and launches the fleet. Open the `critterwatch` resource's endpoint
  to see the dashboard; the fleet appears under Services after each service's first heartbeat. The DLQ and
  Scheduled panels populate because the durable store IS the database.
- **Standalone:** `docker compose up -d` a Postgres (`5432`), then `dotnet run` any project. The localhost
  fallback in `SampleConnections` takes over.

## Tests

```bash
dotnet test                                       # needs a running Docker daemon (Aspire starts Postgres)
dotnet test --filter "Category!=DockerRequired"   # skip the container battery
```

`Tests/FleetSmokeTests.cs` boots the AppHost once, asserts `GET /api/critterwatch/about` → 200, then polls
`GET /api/critterwatch/services` until `TripService`, `RepairShop`, `TripPublisher`, `IncidentService`, and
`IncidentPublisher` have all registered — the shared-schema delivery proof.

## Packages

Consumes the **published** CritterWatch NuGets (`CritterWatch`, `Wolverine.CritterWatch`) and the
DB-transport `WolverineFx.Postgresql` — never a project reference into the CritterWatch repo. Versions are
centrally pinned in `../Directory.Packages.props` (Central Package Management is on for this island),
resolved from the local feed via the gitignored `../nuget.config` until CritterWatch 1.0 publishes to
nuget.org. Note the DB-queue control channel depends on the local `WolverineFx.Postgresql 6.14.1-cw3252`
pin (#531 / `wolverine#3248`), not nuget.org.

## Notes / deviations

- **No broker, one container.** The defining trait of this sample — lean into it. The same Postgres is the
  event store and the Wolverine queue host.
- **Explicit routing, separate control queues.** Because the Postgres transport has no conventional
  routing, business commands are routed explicitly and kept on different queues than CritterWatch operator
  control traffic.
- **No chaos-monkey / partitioning machinery** and **no OpenTelemetry exporter wiring** — same minimalism
  as the flagship (per the plan's "keep it minimal" gotcha; OTel is deferred).
- Doc-embeddable regions are marked with `// begin-snippet: <name>` / `// end-snippet` for the docs
  scraper (see plan `08-docs-snippet-scrape.md`).
```
