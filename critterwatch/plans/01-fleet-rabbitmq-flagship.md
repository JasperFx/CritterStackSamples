# 01 — `Fleet.RabbitMq` (flagship)

**Read `plans/README.md` first.** This is the template-defining solution: build it carefully and
completely, because `02`–`06` copy its structure. Demonstrates a **standalone CritterWatch console**
monitoring a **fleet** (Trip trio + Incidents group) over **RabbitMQ**, all storage on Marten/Postgres,
with Aspire provisioning the RabbitMQ + Postgres containers.

## Prerequisite reading (ground truth — read before writing code)

Do not guess the control-channel wiring. Read these in `~/code/CritterWatch`:
- `src/Smoke/**/Program.cs` — the standalone console host that consumes the packed nuget.
- `src/Samples/Trips/TripService/Program.cs` — how a monitored service wires `UseRabbitMq(...)` +
  `AddCritterWatchMonitoring(critterWatchUri, controlUri)` and which queue URIs it targets.
- `src/Samples/Trips/{TripPublisher,RepairShop,TripMessages}` — the rest of the trio.
- `src/Samples/Incidents/{Incidents.Domain,Incidents.Service,Incidents.Publisher}`.
- `src/BffHost/Program.cs` + `src/BffHost/Composition/SampleExtensions.cs` — how the console side
  registers the RabbitMQ listener/control queue and how license env is propagated to services.
- `Wolverine.CritterWatch/CritterWatchOptions.cs` — the monitored-side options (URIs, MetricsDataSource).

Extract the **exact** RabbitMQ control-channel convention (queue names, the CritterWatch serializer,
leader-only control queue) from those files rather than inventing it.

## Folder layout

```
critterwatch/Fleet.RabbitMq/
  Fleet.RabbitMq.sln
  AppHost/                       # Aspire — provisions rabbitmq + postgres, launches all below
  CritterWatchConsole/           # standalone console: AddCritterWatch + UseCritterWatch
  TripMessages/                  # shared message contracts
  TripService/                   # event-sourced Marten service (monitored)
  TripPublisher/                 # console driver
  RepairShop/                    # second monitored service
  Incidents.Domain/              # aggregates/events/projection
  Incidents.Service/             # monitored, async IncidentsByCategory projection (for rebuild demo)
  Incidents.Publisher/           # console driver
  Tests/                         # Aspire.Hosting.Testing battery (see 07)
  README.md
```

## Build steps

1. **Projects.** Port the trio + Incidents from the source samples. Swap every
   `<ProjectReference Include="...Wolverine.CritterWatch...">` for a CPM `<PackageReference Include="Wolverine.CritterWatch" />`
   (version comes from `Directory.Packages.props`). Service projects reference: `Wolverine.CritterWatch`,
   `WolverineFx.RabbitMQ`, `WolverineFx.Marten`, `Marten`.

2. **CritterWatchConsole.** Model on `src/Smoke` but configure the **RabbitMQ control channel** instead of
   the Postgres transport: `AddCritterWatch(consoleConnString, configureWolverine: opts => { opts.UseRabbitMq(...); /* listen on the CritterWatch control/telemetry queue per the convention you extracted */ })`,
   then `app.UseCritterWatch()`. The console's own storage is Postgres. Annotate heavily.

3. **Monitored services.** Each Trip/Incidents service: `UseRabbitMq(...)` + `AddCritterWatchMonitoring(critterWatchUri, controlUri)`
   pointing at the console's queues. Keep the durable inbox/outbox config from the source samples.

4. **AppHost (the F5 surface).** This is the main *new* work — the existing BffHost provisions only Redis,
   so you build full container provisioning here:
   ```csharp
   var builder = DistributedApplication.CreateBuilder(args);
   var rabbit   = builder.AddRabbitMQ("rabbitmq");                 // Aspire.Hosting.RabbitMQ
   var postgres = builder.AddPostgres("postgres");                 // Aspire.Hosting.PostgreSQL
   var db       = postgres.AddDatabase("critterwatch");

   var console = builder.AddProject<Projects.CritterWatchConsole>("critterwatch")
       .WithReference(rabbit).WaitFor(rabbit)
       .WithReference(db).WaitFor(db)
       .WithExternalHttpEndpoints();

   foreach (var svc in new[]{ "TripService","RepairShop","Incidents_Service" })
       builder.AddProject(...)/* each */.WithReference(rabbit).WaitFor(rabbit).WaitFor(console);
   builder.AddProject<Projects.TripPublisher>("trip-publisher").WaitFor(/* TripService */);
   builder.AddProject<Projects.Incidents_Publisher>("incidents-publisher").WaitFor(/* Incidents.Service */);

   builder.Build().Run();
   ```
   - Resolve each project's connection string / broker URI from the Aspire-injected
     `ConnectionStrings__rabbitmq` / `ConnectionStrings__critterwatch` env (read them in each `Program.cs`
     with a localhost fallback so the project still runs standalone).
   - Propagate `JASPERFX__LICENSEKEY` from host env to every project resource (copy the pattern from
     `src/BffHost/Program.cs`) so operator actions work.
   - `csproj` references `Aspire.Hosting.AppHost`, `Aspire.Hosting.RabbitMQ`, `Aspire.Hosting.PostgreSQL`,
     and `<ProjectReference>`s to every launched project (Aspire needs them for the `Projects.*` metadata).

5. **Tests.** Use the shared harness from `07`. Assert: AppHost reaches `Running`; `GET /api/critterwatch/about`
   → 200; `GET /api/critterwatch/services` → 200 and includes TripService + RepairShop + Incidents.Service
   (poll with a timeout — services self-register asynchronously after first heartbeat).

6. **README + annotations + snippet regions.**

## Gotchas

- The console must be **leader** to own the control queue; on a single-node sample that's automatic, but
  confirm the heartbeat/liveness dot shows (see CritterWatch memory on Solo heartbeat #510 if it doesn't).
- Services register **asynchronously** — tests must poll `/services`, not assert immediately.
- Keep the sample minimal but real: durable inbox/outbox on, but don't pull in unrelated demo machinery
  (chaos monkey, partitioning) — those belong to other samples.

## Template gotchas proven at runtime (the flagship hit these — your clone MUST avoid them)

1. **AppHost resource-name uniqueness.** Aspire resource names are unique case-insensitive *across types*.
   Do NOT name the Postgres database the same as the console project. Pattern used:
   `postgres.AddDatabase("critterstore", "critterwatch")` — resource `critterstore`, actual DB `critterwatch`.
   The **console PROJECT** keeps the name `critterwatch` (the shared harness waits on that exact resource name).
2. **The console needs an HTTP launch profile.** A `Microsoft.NET.Sdk.Web` project with no
   `Properties/launchSettings.json` exposes no endpoint → Aspire `CreateHttpClient` throws
   `Endpoint '' ... not found`. Ship `CritterWatchConsole/Properties/launchSettings.json` with a single
   **http** profile (`"applicationUrl": "http://localhost:<port>"`). The shared harness now calls
   `CreateHttpClient("critterwatch", "http")`, so keep the endpoint named `http`.
3. The connection-string env key follows the **DB resource name** (`ConnectionStrings__critterstore`), not the
   database name — keep `Fleet.Common.SampleConnections` in sync with the AppHost resource name.
4. **Aspire resource names are DNS-style: letters/digits/HYPHENS only — NO underscores**, and unique
   case-insensitive across all types. When a transport entity is an Aspire resource (e.g. ASB
   `AddServiceBusQueue`), use the 2-arg/3-arg overload: a hyphenated **resource** name + the real wire name
   (which may contain underscores) as the **entity** name. Never name such a resource `critterwatch` (the
   console project owns it) or `trip_service` (underscore → invalid). Broker queues that are NOT Aspire
   resources (RabbitMQ/SQS queue strings) are unaffected.

✅ **Flagship verified green:** `dotnet test` boots real RabbitMQ + Postgres and both smoke tests pass (~14s).

## Acceptance: the `Definition of done` in `plans/README.md`, all 5 points.
