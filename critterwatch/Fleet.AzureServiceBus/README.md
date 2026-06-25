# Fleet.AzureServiceBus — CritterWatch over Azure Service Bus (emulator)

A standalone **CritterWatch** monitoring console watching a small **fleet** of Wolverine services over
**Azure Service Bus**, with all event storage on **Marten / Postgres**. Everything is provisioned by a
.NET Aspire AppHost — including the **Azure Service Bus emulator** — so you clone, open
`Fleet.AzureServiceBus.sln`, and press **F5**.

This is the [`Fleet.RabbitMq`](../Fleet.RabbitMq) flagship with **only the transport swapped** to Azure
Service Bus. The fleet is the **Trip trio only** (no Incidents group, per the samples plan).

## What's in the box

| Project | Role |
|---------|------|
| `AppHost` | .NET Aspire host. Provisions the **Azure Service Bus emulator** (+ the backing SQL container Aspire wires for it) and a **Postgres** container, declares the fleet's ASB queues, and launches everything below. The F5 surface. |
| `CritterWatchConsole` | The standalone monitoring dashboard. `AddCritterWatch(...)` + `UseCritterWatch()`. Owns its own Postgres store; listens for the fleet on the ASB control channel. |
| `TripMessages` | Shared command/event contracts for the Trip trio (transport-agnostic — identical to the flagship). |
| `TripService` | Event-sourced Marten service. Owns the `Trip` aggregate + three async projections + a no-op subscription. Monitored. |
| `TripPublisher` | Console driver — synthesizes trip traffic and keeps it flowing via the `ContinueTrip` ping-pong. Monitored. |
| `RepairShop` | Second Marten service. Handles `RepairRequested` from TripService and replies `RepairsCompleted`. Monitored. |
| `Fleet.Common` | One tiny helper: resolves the ASB + Postgres connections from Aspire config (or a localhost / emulator fallback). |
| `Tests` | `Aspire.Hosting.Testing` smoke battery — boots the AppHost and asserts the console is up and the fleet registers. |

## The Azure Service Bus control channel (the wiring to copy)

Every monitored service reports to the console over one shared ASB queue (`critterwatch`). Two
transport-agnostic levers carry the control channel — the same two as every other Fleet sample:

**Console side** (`CritterWatchConsole/Program.cs`):

```csharp
opts.UseAzureServiceBus(asbConnectionString);

var asb = opts.Transports.GetOrCreate<AzureServiceBusTransport>();
asb.SystemQueuesEnabled = false;            // stay under the emulator's 50-entity cap

opts.ListenToAzureServiceBusQueue("critterwatch")  // THE shared control/telemetry queue
    .ListenOnlyAtLeader()                            // one console node owns it (no split-brain)
    .UseCritterWatchSerializer();                    // pin CritterWatch's wire-format (brotli STJ)
```

**Monitored side** (every service's `Program.cs`):

```csharp
opts.UseAzureServiceBus(asbConnectionString);
opts.Transports.GetOrCreate<AzureServiceBusTransport>().SystemQueuesEnabled = false;

opts.ListenToAzureServiceBusQueue("trip_service");  // this service's own inbound control queue

opts.AddCritterWatchMonitoring(
    "asb://queue/critterwatch".ToUri(),   // -> the console's shared queue (telemetry/registration)
    "asb://queue/trip_service".ToUri());  // -> this service's queue (commands route back here)
```

So: the **first** `AddCritterWatchMonitoring` URI always points at `asb://queue/critterwatch` (the
console's queue), and the **second** is the service's own queue, matching its
`ListenToAzureServiceBusQueue(...)`. Each service uses a distinct second queue: `trip_service`,
`trip_publisher`, `repair_shop`.

> **`asb://queue/{name}` is mandatory** (CritterWatch #345). Wolverine's
> `AzureServiceBusTransport.findEndpointByUri` switches on `uri.Host` expecting `queue` or `topic`, so the
> bare `asb://{name}` form throws `ArgumentOutOfRangeException` at host build.

## ASB emulator constraints (the interesting transport-specific part)

The Azure Service Bus emulator caps a namespace at **50 entities** and **does not support runtime
AutoProvision** — it loads its entity set from a config at container start. This sample handles both:

1. **Entities are declared up front in the AppHost**, not provisioned at runtime. `AppHost/Program.cs`
   calls `serviceBus.AddServiceBusQueue(name)` once per queue the fleet uses — `critterwatch`,
   `trip_service`/`trip_service_app`, `trip_publisher`/`trip_publisher_app`,
   `repair_shop`/`repair_shop_app` (7 queues, well under the cap). Each maps 1:1 to a
   `ListenTo.../PublishMessage(...).To...` call in a service.
2. **`SystemQueuesEnabled = false`** on every Wolverine host drops the per-node response/retry/control
   system queues that would otherwise blow past the cap.
3. **Explicit routing replaces `UseConventionalRouting()`** — conventional routing mints ~one ASB queue
   per message type. Instead the services route the few cross-service messages they produce to a handful
   of `*_app` inbox queues.

Because Aspire pre-declares the entities, the Wolverine side sets **no** `ManagementConnectionString` and
calls **no** `.AutoProvision()` (unlike the docker-compose `Trips2` sample this is ported from, which
provisioned at runtime against a separate management port). The ASB topology is treated as externally
owned by the emulator.

## Connection resolution

`Fleet.Common/SampleConnections.cs` reads `ConnectionStrings__messaging` (the Aspire ASB emulator) /
`ConnectionStrings__critterstore` (the Postgres DB), injected by Aspire's `.WithReference(...)`. Standalone
(no Aspire), it falls back to the well-known ASB **development-emulator** connection string and the
localhost docker-compose Postgres, so any project still runs standalone against a manually-started emulator.

## Running

- **F5 / Aspire (recommended):** open `Fleet.AzureServiceBus.sln`, run `AppHost`. Aspire pulls the ASB
  emulator image (and its backing SQL container) + the Postgres image, declares the queues, starts the
  console, and launches the fleet. Open the `critterwatch` resource's endpoint to see the dashboard; the
  fleet appears under Services after each service's first heartbeat. (The ASB emulator + its SQL backing
  container take longer than RabbitMQ to warm up on first run.)
- **Standalone:** start the ASB emulator + a Postgres (`5432`) yourself, then `dotnet run` any project. The
  fallbacks in `SampleConnections` take over (you'll need to create the queues out-of-band, since the
  services don't AutoProvision).

## Tests

```bash
dotnet test                                   # needs a running Docker daemon (Aspire starts containers)
dotnet test --filter "Category!=DockerRequired"   # skip the container battery
```

`Tests/FleetSmokeTests.cs` boots the AppHost once, asserts `GET /api/critterwatch/about` → 200, then polls
`GET /api/critterwatch/services` until `TripService`, `RepairShop`, and `TripPublisher` have registered.

## Packages

Consumes the **published** CritterWatch NuGets (`CritterWatch`, `Wolverine.CritterWatch`) +
`WolverineFx.AzureServiceBus` — never a project reference into the CritterWatch repo. Versions are
centrally pinned in `../Directory.Packages.props` (Central Package Management is on for this island),
resolved from the local feed via the gitignored `../nuget.config` until CritterWatch 1.0 publishes to
nuget.org.

## Notes / deviations

- **Trip trio only — no Incidents group.** Per the samples plan, the Incidents group ships only in the
  flagship + the DB-queue fleets; transport-swap fleets run the Trip trio.
- **DLQ + Scheduled panels read CritterWatch's durable `IMessageStore`, not a broker-native DLQ.** Each
  service is paired with a durable Marten/Postgres store so those panels populate — the ASB transport
  governs only the control/telemetry path.
- **No OpenTelemetry exporter wiring.** OTel tracing is deferred from the initial battery; the services
  skip the `UseOtlpExporter()` / Prometheus plumbing the in-repo `Trips2` sample carries.
- Doc-embeddable regions are marked with `// begin-snippet: <name>` / `// end-snippet` for the docs
  scraper (see plan `08-docs-snippet-scrape.md`).
```
