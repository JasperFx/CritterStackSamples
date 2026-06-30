# Fleet.GooglePubSub — CritterWatch over Google Cloud Pub/Sub (emulator)

A standalone **CritterWatch** monitoring console watching a small **fleet** of Wolverine services over
**Google Cloud Pub/Sub**, with all event storage on **Marten / Postgres**. Everything is provisioned by a
.NET Aspire AppHost — including the **Pub/Sub emulator** — so you clone, open
`Fleet.GooglePubSub.sln`, and press **F5**.

This is the [`Fleet.RabbitMq`](../Fleet.RabbitMq) flagship with **only the transport swapped** to Google
Cloud Pub/Sub. The fleet is the **Trip trio only** (no Incidents group, per the samples plan).

## What's in the box

| Project | Role |
|---------|------|
| `AppHost` | .NET Aspire host. Provisions the **Pub/Sub emulator** (as a plain container — Pub/Sub has no first-class Aspire resource) and a **Postgres** container, and launches everything below. The F5 surface. |
| `CritterWatchConsole` | The standalone monitoring dashboard. `AddCritterWatch(...)` + `UseCritterWatch()`. Owns its own Postgres store; listens for the fleet on the Pub/Sub control channel. |
| `TripMessages` | Shared command/event contracts for the Trip trio (transport-agnostic — identical to the flagship). |
| `TripService` | Event-sourced Marten service. Owns the `Trip` aggregate + three async projections + a no-op subscription. Monitored. |
| `TripPublisher` | Console driver — synthesizes trip traffic and keeps it flowing via the `ContinueTrip` ping-pong. Monitored. |
| `RepairShop` | Second Marten service. Handles `RepairRequested` from TripService and replies `RepairsCompleted`. Monitored. |
| `Fleet.Common` | One tiny helper: resolves the Pub/Sub project id + Postgres connection from Aspire config (or a localhost / emulator fallback). |
| `Tests` | `Aspire.Hosting.Testing` smoke battery — boots the AppHost and asserts the console is up and the fleet registers. |

## The Pub/Sub control channel (the wiring to copy)

Every monitored service reports to the console over one shared Pub/Sub topic (`critterwatch`). The same two
transport-agnostic levers carry the control channel as every other Fleet sample:

**Console side** (`CritterWatchConsole/Program.cs`):

```csharp
opts.UsePubsub(projectId)
    .UseEmulatorDetection(EmulatorDetection.EmulatorOrProduction)  // talk to the emulator when PUBSUB_EMULATOR_HOST is set
    .AutoProvision();                                              // create topics + subscriptions (emulator starts empty)

opts.ListenToPubsubTopic("critterwatch")   // THE shared control/telemetry topic
    .ListenOnlyAtLeader();                  // one console node owns the subscription (no split-brain)
```

**Monitored side** (every service's `Program.cs`):

```csharp
opts.UsePubsub(projectId)
    .UseEmulatorDetection(EmulatorDetection.EmulatorOrProduction)
    .AutoProvision();

opts.ListenToPubsubTopic("trip_service");  // this service's own inbound control topic

opts.AddCritterWatchMonitoring(
    GcpPubsubEndpointUri.Topic(projectId, "critterwatch"),   // -> the console's shared topic (telemetry/registration)
    GcpPubsubEndpointUri.Topic(projectId, "trip_service"));  // -> this service's topic (commands route back here)
```

So: the **first** `AddCritterWatchMonitoring` URI always points at `pubsub://{projectId}/critterwatch` (the
console's topic), and the **second** is the service's own topic, matching its `ListenToPubsubTopic(...)`.
Each service uses a distinct second topic: `trip_service`, `trip_publisher`, `repair_shop`.
`GcpPubsubEndpointUri.Topic(projectId, name)` builds the canonical `pubsub://{projectId}/{name}` URI form.

> **Why `ListenOnlyAtLeader()` needs wolverine#3258.** A leader-pinned Pub/Sub listener must read from ONE
> shared, cluster-stable subscription. Before the fix, `PubsubEndpoint.SetupAsync` appended the assigned
> node number to the subscription name for *every* listener, so each node created its own subscription —
> and Pub/Sub fans a copy of every message to each subscription, breaking the single-consumer (leader-only)
> guarantee the control channel relies on. The fix only appends the per-node suffix for `CompetingConsumers`
> listeners. **This shipped in WolverineFx 6.16.0**, which the sample consumes directly.

## Pub/Sub emulator constraints (the interesting transport-specific part)

Unlike the Azure Service Bus emulator (50-entity cap, no runtime provisioning, pre-declared topology), the
Pub/Sub emulator starts **empty** and **does** support the admin RPCs, so the topology is provisioned at
runtime by Wolverine:

1. **`.AutoProvision()`** on every Wolverine host creates the `critterwatch` topic, each `*_app` routing
   topic, each service's control topic, and the pull subscriptions — there is nothing to pre-declare in the
   AppHost.
2. **The emulator is a plain container.** Aspire has no first-class Pub/Sub resource, so `AppHost/Program.cs`
   uses `AddContainer("pubsub", "gcr.io/google.com/cloudsdktool/google-cloud-cli", "emulators")` and starts
   it with `gcloud beta emulators pubsub start`. Each project gets `PUBSUB_EMULATOR_HOST` pointed at it —
   that single env var is the only switch between the emulator and real Pub/Sub.
3. **Explicit routing replaces `UseConventionalRouting()`** — same as the flagship, the few cross-service
   messages are routed to a handful of `*_app` inbox topics, keeping the topology legible.

## Connection resolution

`Fleet.Common/SampleConnections.cs` resolves the shared **GCP project id** (`critterwatch-sample`, matching
the emulator's `--project`) and reads `ConnectionStrings__critterstore` (the Postgres DB) injected by
Aspire's `.WithReference(...)`. The emulator endpoint flows in via the `PUBSUB_EMULATOR_HOST` env var the
AppHost sets — there is no Pub/Sub "connection string". Standalone (no Aspire), it falls back to the
localhost docker-compose Postgres; set `PUBSUB_EMULATOR_HOST=localhost:8085` against a manually-started
emulator to run a project on its own.

## Running

- **F5 / Aspire (recommended):** open `Fleet.GooglePubSub.sln`, run `AppHost`. Aspire pulls the Pub/Sub
  emulator image + the Postgres image, starts the emulator, starts the console, and launches the fleet.
  Open the `critterwatch` resource's endpoint to see the dashboard; the fleet appears under Services after
  each service's first heartbeat. (The `google-cloud-cli:emulators` image is large, so the **first** run
  pulls slowly.)
- **Standalone:** start the Pub/Sub emulator (`gcloud beta emulators pubsub start --host-port=localhost:8085
  --project=critterwatch-sample`) + a Postgres (`5432`) yourself, set `PUBSUB_EMULATOR_HOST=localhost:8085`,
  then `dotnet run` any project. `.AutoProvision()` creates the topics/subscriptions for you.

## Tests

```bash
dotnet test                                   # needs a running Docker daemon (Aspire starts containers)
dotnet test --filter "Category!=DockerRequired"   # skip the container battery
```

`Tests/FleetSmokeTests.cs` boots the AppHost once, asserts `GET /api/critterwatch/about` → 200, then polls
`GET /api/critterwatch/services` until `TripService`, `RepairShop`, and `TripPublisher` have registered.

## Packages

Consumes the **published** CritterWatch NuGets (`CritterWatch`, `Wolverine.CritterWatch`) +
`WolverineFx.Pubsub 6.16.0` (which carries the wolverine#3258 fix — see the control-channel note above) —
never a project reference into the CritterWatch repo. Versions are centrally pinned in
`../Directory.Packages.props` (Central Package Management is on for this island); the CritterWatch packages
resolve from the local feed via the gitignored `../nuget.config`, everything else from nuget.org.

## Notes / deviations

- **Trip trio only — no Incidents group.** Per the samples plan, the Incidents group ships only in the
  flagship + the DB-queue fleets; transport-swap fleets run the Trip trio.
- **No OpenTelemetry exporter wiring.** OTel tracing is deferred from the initial battery.
- Doc-embeddable regions are marked with `// begin-snippet: <name>` / `// end-snippet` for the docs
  scraper (see plan `08-docs-snippet-scrape.md`).
