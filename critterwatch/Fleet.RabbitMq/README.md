# Fleet.RabbitMq — CritterWatch flagship sample

A standalone **CritterWatch** monitoring console watching a small **fleet** of Wolverine services over
**RabbitMQ**, with all event storage on **Marten / Postgres**. Everything is provisioned by a .NET Aspire
AppHost: clone, open `Fleet.RabbitMq.sln`, press **F5**.

This is the *flagship* sample — the template every other Fleet/Embedded/WebService sample copies.

## What's in the box

| Project | Role |
|---------|------|
| `AppHost` | .NET Aspire host. Provisions the **RabbitMQ** + **Postgres** containers and launches everything below. The F5 surface. |
| `CritterWatchConsole` | The standalone monitoring dashboard. `AddCritterWatch(...)` + `UseCritterWatch()`. Owns its own Postgres store; listens for the fleet on the RabbitMQ control channel. |
| `TripMessages` | Shared command/event contracts for the Trip trio. |
| `TripService` | Event-sourced Marten service. Owns the `Trip` aggregate + three async projections + a no-op subscription. Monitored. |
| `TripPublisher` | Console driver — synthesizes trip traffic and keeps it flowing via the `ContinueTrip` ping-pong. Monitored. |
| `RepairShop` | Second Marten service. Handles `RepairRequested` from TripService and replies `RepairsCompleted`. Monitored. |
| `Incidents.Domain` | Shared aggregates / events / commands + the `IncidentsByCategory` multi-stream projection. |
| `Incidents.Service` | Marten service — snapshots `Incident` inline, runs `IncidentsByCategory` async (a rebuild-UI target), schedules demo reminders. Monitored. |
| `Incidents.Publisher` | Console driver for incident traffic. Monitored. |
| `Fleet.Common` | One tiny helper: resolves the RabbitMQ + Postgres connections from Aspire config (or a localhost fallback). |
| `Tests` | `Aspire.Hosting.Testing` smoke battery — boots the AppHost and asserts the console is up and the fleet registers. |

## The RabbitMQ control channel (the wiring to copy)

Every monitored service reports to the console over one shared RabbitMQ queue. The convention:

**Console side** (`CritterWatchConsole/Program.cs`):

```csharp
opts.UseRabbitMq(brokerUri).DisableDeadLetterQueueing().AutoProvision();

opts.ListenToRabbitQueue("critterwatch")   // THE shared control/telemetry queue
    .ListenOnlyAtLeader()                   // one console node owns it (no split-brain)
    .UseCritterWatchSerializer();           // pin CritterWatch's wire-format
```

**Monitored side** (every service's `Program.cs`):

```csharp
opts.UseRabbitMq(brokerUri).DisableDeadLetterQueueing().UseConventionalRouting().AutoProvision();
opts.ListenToRabbitQueue("trip_service");   // this service's own inbound control queue

opts.AddCritterWatchMonitoring(
    "rabbitmq://queue/critterwatch".ToUri(),   // -> the console's shared queue (telemetry/registration)
    "rabbitmq://queue/trip_service".ToUri());  // -> this service's queue (commands route back here)
```

So: the **first** `AddCritterWatchMonitoring` URI always points at `rabbitmq://queue/critterwatch` (the
console's queue), and the **second** is the service's own queue, matching its `ListenToRabbitQueue(...)`.
Each service uses a distinct second queue: `trip_service`, `trip_publisher`, `repair_shop`,
`incident_service`, `incident_publisher`.

## Connection resolution

`Fleet.Common/SampleConnections.cs` reads `ConnectionStrings__rabbitmq` / `ConnectionStrings__critterwatch`
(injected by Aspire's `.WithReference(...)`), falling back to the localhost docker-compose broker/db so any
project still runs standalone.

## Running

- **F5 / Aspire (recommended):** open `Fleet.RabbitMq.sln`, run `AppHost`. Aspire pulls the RabbitMQ +
  Postgres images, starts the console, and launches the fleet. Open the `critterwatch` resource's endpoint
  to see the dashboard; the fleet appears under Services after each service's first heartbeat.
- **Standalone:** `docker compose up -d` a RabbitMQ (`5672`) + Postgres (`5432`), then `dotnet run` any
  project. The localhost fallbacks in `SampleConnections` take over.

## Tests

```bash
dotnet test                                   # needs a running Docker daemon (Aspire starts containers)
dotnet test --filter "Category!=DockerRequired"   # skip the container battery
```

`Tests/FleetSmokeTests.cs` boots the AppHost once, asserts `GET /api/critterwatch/about` → 200, then polls
`GET /api/critterwatch/services` until `TripService`, `RepairShop`, and `IncidentService` have registered.

## Packages

Consumes the **published** CritterWatch NuGets (`CritterWatch`, `Wolverine.CritterWatch`) — never a project
reference into the CritterWatch repo. Versions are centrally pinned in `../Directory.Packages.props`
(Central Package Management is on for this island), resolved from the local feed via the gitignored
`../nuget.config` until CritterWatch 1.0 publishes to nuget.org.

## Notes / deviations

- **No chaos-monkey / partitioning machinery.** The upstream Trip sample injects chaos failures and a
  `CleanTripProjection` twin for CritterWatch's own E2E tests; those are deliberately omitted here to keep
  the teaching sample minimal and real (per the plan's "keep it minimal" gotcha).
- **No OpenTelemetry exporter wiring.** OTel tracing is deferred from the initial battery; the services
  skip the `UseOtlpExporter()` plumbing the in-repo samples carry.
- Doc-embeddable regions are marked with `// begin-snippet: <name>` / `// end-snippet` for the docs
  scraper (see plan `08-docs-snippet-scrape.md`).
