# WebService.Http

CritterWatch monitoring a **Wolverine HTTP web service** — where the monitoring link itself rides
**Wolverine's HTTP transport** instead of a message broker. Storage is Marten/Postgres.

This is the sample to read when your service exposes an HTTP API and you'd rather not stand up a broker
just to get it onto the CritterWatch dashboard.

## What it shows

- A small **event-sourced Orders API** (`OrderService`) built on **WolverineFx.Http + Marten**.
- That service reporting itself to a standalone **CritterWatch console** over **Wolverine's HTTP transport**
  (`POST /_wolverine/invoke`) — no RabbitMQ, no database queue, just HTTP.
- Its non-Wolverine ASP.NET endpoints surfaced on CritterWatch's HTTP tab via `AddCritterWatchHttp()` (#538).
- **Aspire** provisioning a single Postgres container (the only infrastructure dependency) and launching
  both processes.

## Layout

```
WebService.Http/
  WebService.Http.sln
  AppHost/               # Aspire — provisions Postgres, launches the console + OrderService
  CritterWatchConsole/   # standalone console; RECEIVES telemetry over the HTTP transport
  Fleet.Common/          # connection + console-URL resolution (Aspire env, localhost fallback)
  OrderService/          # the monitored Wolverine HTTP web service (event-sourced Orders API)
  Tests/                 # Aspire.Hosting.Testing battery
  README.md
```

## How the HTTP-transport monitoring link works (push)

The monitoring link is **push**: the monitored service is the HTTP *client*, the console is the HTTP
*server*. (Confirmed from `Wolverine.Http.Transport`: `ToHttpEndpoint()` is the publish side and
`MapWolverineHttpTransportEndpoints()` is the receive side — the sender POSTs Wolverine envelopes to the
receiver's HTTP endpoint.)

**OrderService (sender)** — `OrderService/Program.cs`:
- `AddCritterWatchMonitoring(telemetryUri, controlUri)` with `telemetryUri = {console}/_wolverine/invoke`.
  Because the URI scheme is `http`/`https`, the monitoring routes send **inline** — each registration /
  heartbeat / telemetry envelope is a POST to the console's invoke route.
- The HTTP transport sender needs `IWolverineHttpTransportClient` + a **named** `HttpClient` whose name equals
  the telemetry URI string and whose `BaseAddress` is that URI (the transport does
  `IHttpClientFactory.CreateClient(outboundUri)` and POSTs to `client.BaseAddress`).
- `MapWolverineHttpTransportEndpoints()` so the console can POST operator commands **back** to this service.

**CritterWatchConsole (receiver)** — `CritterWatchConsole/Program.cs`:
- `AddCritterWatch(...)` for the dashboard/store, then `app.MapWolverineHttpTransportEndpoints()` to receive
  envelopes at `/_wolverine/invoke`, executed inline against the console's CritterWatch handlers.
- Its Wolverine **default serializer** is pinned to `BuildCritterWatchSerializer()`. This is the
  HTTP-transport equivalent of the broker fleets' `.UseCritterWatchSerializer()` on the inbound queue: the
  CritterWatch serializer reports content-type `application/json` but frames its body with a Brotli prefix,
  so the console must resolve `application/json` to the CritterWatch serializer (not the stock JSON one) for
  the inbound bytes to decode.

> The two sides talk over plain HTTP — make sure the console's address is reachable from the service.
> Under Aspire, `.WithReference(console)` injects the console's URL via service discovery
> (`services__critterwatch__http__0`); `Fleet.Common.SampleConnections.ConsoleBaseUrl()` reads it with a
> localhost fallback.

## Run it

```bash
# from the repo root
cd critterwatch/WebService.Http
dotnet run --project AppHost      # or open WebService.Http.sln and press F5
```

Aspire starts Postgres, the console, and the OrderService. Open the Aspire dashboard, then the console's
dashboard — `OrderService` appears under Services once it registers. Drive some traffic:

```bash
# place an order (201 Created + Location)
curl -i -X POST http://localhost:5291/orders \
  -H 'content-type: application/json' \
  -d '{"customer":"Acme","items":["widget","gadget"]}'
```

## Tests

```bash
dotnet test                                 # needs Docker (Postgres container via Aspire)
dotnet test --filter "Category!=DockerRequired"   # skip the container battery
```

The battery boots the AppHost and asserts: the console's `GET /api/critterwatch/about` is 200; the
OrderService's own endpoints respond (`GET /` and `POST /orders`); and `OrderService` shows up in
`GET /api/critterwatch/services` (polled — registration is asynchronous).

## Notes / template gotchas (inherited from the flagship)

- The Postgres **DB resource** is named `critterstore`, not `critterwatch`, because the console **project**
  owns the `critterwatch` resource name and Aspire resource names are unique case-insensitive across types.
  `Fleet.Common` reads `ConnectionStrings__critterstore` to match.
- The console and the OrderService are both Web SDK projects, so each ships a
  `Properties/launchSettings.json` with a single **`http`** profile — the shared harness calls
  `CreateHttpClient(resource, "http")` and Aspire needs a named `http` endpoint to exist.
- CritterWatch needs **Balanced** durability (not Solo) for projection pause/restart/rebuild to work on a
  single node — see the comment in `OrderService/Program.cs`.
