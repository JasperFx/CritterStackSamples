# CritterWatch samples

F5-able sample solutions for [CritterWatch](https://jasperfx.net) — the monitoring
and production-management console for the Critter Stack (Wolverine + Marten/Polecat).
Each solution is **self-contained**: clone, open the `.sln`, press **F5**. The Aspire
AppHost provisions every Docker dependency (Postgres / RabbitMQ / SQL Server / Kafka /
Redis / emulators) — no manual `docker compose` step.

## Prerequisites

- .NET 10 SDK
- Docker (running) — Aspire starts the containers for you
- The [.NET Aspire workload / SDK](https://learn.microsoft.com/dotnet/aspire) (`Aspire.AppHost.Sdk` 9.5.0)

## Pre-release packages (local feed)

CritterWatch 1.0 has not shipped to nuget.org yet, so these samples currently restore
the CritterWatch `1.0.0-alpha.4` packages (and a handful of `-cw*` Wolverine pins) from
a **local feed**. The machine-specific `nuget.config` here is **gitignored**.

To build locally you need that feed populated. From the CritterWatch repo:

```bash
# packs CritterWatch, CritterWatch.SqlServer, Wolverine.CritterWatch(.Http),
# CritterWatch.Abstractions into ~/code/_cw_localfeed at the pinned label
dotnet pack <project> -c Release -p:Version=1.0.0-alpha.4 --output ~/code/_cw_localfeed
```

Then create `critterwatch/nuget.config` (gitignored) pointing at that feed — see the
committed example in repo history / ask a maintainer. Once 1.0 publishes to nuget.org,
this step disappears and the samples become true clone-and-F5.

## Layout

| Solution | Demonstrates |
|----------|--------------|
| `Fleet.RabbitMq` | Standalone CritterWatch console monitoring a fleet (Trips + Incidents) over RabbitMQ ⭐ flagship |
| `Fleet.AmazonSqs` / `.AzureServiceBus` / `.Kafka` / `.Redis` / `.GooglePubSub` | Same fleet over each transport |
| `Fleet.PostgresqlQueues` / `.SqlServerQueues` | DB-backed queue transport (also the standalone Marten / Polecat storage showcases) |
| `Embedded.Sqlite` | **CritterWatch embedded in your own app**, on SQLite — no broker, no database server, no containers |
| `WebService.Http` | Monitoring a Wolverine HTTP service over the HTTP transport |
| `Metrics.Prometheus` | Services in `SystemDiagnosticsMeter` mode; CritterWatch polls Prometheus |

Shared pins live in `Directory.Packages.props`; shared build settings in
`Directory.Build.props`. Each solution carries an Aspire-launch + route-smoke test
battery (`Aspire.Hosting.Testing`).
