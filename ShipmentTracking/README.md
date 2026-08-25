# ShipmentTracking

A Wolverine service converted from NServiceBus. All five phases of a migration walkthrough:
NServiceBus → Wolverine → Wolverine.HTTP → Polecat → integration tests → Aspire + CritterWatch.

The NServiceBus starting point is **not** in this repository. NServiceBus is RPL-1.5
licensed and this repo is MIT, so the "before" state was written from scratch against
NServiceBus' public API and lives outside the tree; the write-up quotes it inline.
See the licensing advisories in `../sample-projects.md`.

## What the migration actually decided

The NServiceBus version had **one endpoint**, so it had one concurrency setting and
one recoverability policy for three workloads that want different things. That is not
a criticism of NServiceBus — it is the shape of the model. Nothing asks you which of
the three you meant, because there is only one answer available.

Wolverine asks per listener, and the three answers differ for three different reasons:

| Endpoint | Mode | Decided by |
|---|---|---|
| `carrier-events` | `NativeAck`, globally partitioned | **Throughput cost** — Durable's per-message inbox writes are the ceiling |
| `shipment-commands` | `Durable` | **Delivery guarantee** — the outbox has to carry follow-on events |
| `label-generation` | `Durable`, 4 parallel | **Handler duration** — the axis nobody asks about |

`label-generation` is the interesting one. It looks exactly like `carrier-events` —
same broker, same "we need throughput" complaint — and it is the one endpoint that
must **not** be on `NativeAck`. The carrier label API takes 30–90 seconds, and under
`NativeAck` the broker's clock runs for the whole handler: the lease expires, the
broker redelivers, and the duplicate runs *concurrently with the original*.

## Ordering, and the trap in it

"Scans for one shipment must be processed in order" is not solved by the mode. A
single `carrier-events` queue with competing consumers across nodes has no ordering
guarantee at all, and `PartitionProcessingByGroupId` on the listener would only order
each node's own work — two nodes would still process the same shipment at once.

So the topology is global: `MessagePartitioning.GlobalPartitioned` with sharded
RabbitMQ queues, grouping inferred from `ShipmentId`. One shipment always lands on the
same shard, and only then does ordering hold cluster-wide.

`NativeAck` also means redelivery is expected by design, so `CarrierScanHandler` is
idempotent by construction — the scan update is guarded on being newer than the one
already recorded, so a duplicate is a no-op rather than a regression.

## Conversion notes

| NServiceBus | Here |
|---|---|
| `IHandleMessages<T>` classes | static handler classes; `Handle` found by convention |
| `context.Publish` / `context.Send` | cascading returns — `OutgoingMessages`, or the message itself |
| `Saga<TData>` + `ContainSagaData` | `Saga` base class; state lives on the saga |
| `ConfigureHowToFindSaga` mapper | `[SagaIdentity]` on message properties |
| `IAmStartedByMessages<T>` / `IHandleTimeouts<T>` | `Start(T)` / an ordinary `Handle(T)` — a timeout is just a scheduled message |
| `RequestTimeout<T>(context, delay)` | `DeliverySlaExpired : TimeoutMessage(5.Days())` — the delay is on the message type, so `Start` just returns it |
| `MarkAsComplete()` | `MarkCompleted()` |
| `Behavior<IIncomingLogicalMessageContext>` | **deleted** — see below |
| `IMessageSession` outside handlers | `IMessageBus` |
| `context.MessageHeaders` | `Envelope.Headers` |
| `UsePersistence<SqlPersistence>()` + `EnableOutbox()` | phase 1: `PersistMessagesWithSqlServer(...)`; phase 3: Polecat's `IntegrateWithWolverine()` |
| endpoint-wide `Recoverability()` | `OnException<T>()` policies, per exception type |
| `EnableInstallers()` | `RunJasperFxCommands(args)` |

**The most interesting conversion was a deletion.** The NServiceBus pipeline behavior
pulled a correlation id off the headers and pushed it, with the message type, into a
logging scope around `await next()`.

Wolverine could express that — `Before` can return a value later methods receive, and
`Finally` runs inside a `try/finally`, so a scope opened in `Before` can be disposed in
`Finally` and cover the failure path. But it should not, because the behavior existed
to compensate for something Wolverine already does. Its OpenTelemetry spans carry
`messaging.conversation_id` (the correlation id), `messaging.message_type`,
`messaging.message_id`, `message.handler` and `handler.type` — a superset of what the
behavior was assembling by hand, on a span rather than a log scope.

So the conversion is: delete it, register the `Wolverine` `ActivitySource`, and read the
trace. **Not every behavior wants an equivalent** — some are scaffolding around a gap
the new framework does not have.

## Phase 2 — Minimal API to Wolverine.HTTP

The endpoints were minimal API lambdas that injected `IMessageBus` and called it
explicitly. They are now endpoint methods discovered by `MapWolverineEndpoints()`.

**The explicit bus call is gone the same way `context.Publish` went in phase 1.** A
command is the second element of a tuple return, so Wolverine sends it through the
outbox after the response is written — the same cascading shape the message handlers
already use.

### What a client sees: nothing changed

Every route, verb, status code and response body is identical, including 202 on all
three command routes and the `Location` header on `POST /shipments` only.

**Wolverine ships `AcceptResponse` for 202**, the sibling of `CreationResponse`, in the
same file and on the same `IHttpAware` seam. `ShipmentAccepted` derives from it, so the
status code, the `Location` header and the OpenAPI metadata all come for free:

```csharp
public record ShipmentAccepted(Guid ShipmentId) : AcceptResponse($"/shipments/{ShipmentId}");
```

The two routes that return 202 with **no** body are the exception. `AcceptResponse`
requires a `Url` and always stamps `Location`, and those routes never had one — the
minimal API returned a bare `Results.Accepted()`. They use `TypedResults.Accepted((string?)null)`
instead, which is still a concrete type: `Accepted` implements `IEndpointMetadataProvider`
and Wolverine calls `PopulateMetadata` on any return type that does
(`HttpChain.EndpointBuilder.tryApplyAsEndpointMetadataProvider`), so 202 still reaches
the OpenAPI document. This is not the opaque `IResult` case the docs warn about.

> A wrong turn worth recording, because it is easy to repeat: the first cut of this
> conversion hand-rolled an `AcceptResponse` against `IHttpAware`, having grepped for
> `class AcceptResponse` and found nothing. It is a `record`. Forty lines reimplementing
> a type that already shipped. **Absence of a grep hit is not absence of an API** — and
> in `Wolverine.Http`, response types live together in `IHttpAware.cs`.

### What did change: the OpenAPI document got better

The minimal API returned `Results.Accepted(...)` — the untyped helper, which tells the
generator nothing. The converted endpoints return `Accepted<T>`, so 202 and the response
schema are both documented, and `GET /shipments/{id}` gets 200 and 404 from its nullable
return with no attribute at all. Additive: no client behaviour changes, but a generated
client would now be correct where it previously was not.

### A saga method returns the cascade, not itself

`Start` sets the saga's state and returns **only** what should happen next:

```csharp
public DeliverySlaExpired Start(ShipmentBooked booked)
{
    Id = booked.ShipmentId;
    BookedAt = booked.BookedAt;
    return new DeliverySlaExpired(booked.ShipmentId);
}
```

Returning `(this, cascade)` compiles and appears to work, which is what makes it worth
naming: that tuple shape belongs to an **immutable** saga, or to a `static Start` that
*creates* the instance. On a mutable saga it reads as though the state were being
returned when it is really being mutated in place.

The delay is not here either. `DeliverySlaExpired` subclasses `TimeoutMessage(5.Days())`,
so every instance is scheduled five days out wherever it is returned, and the saga method
stays a pure function naming an outcome rather than scheduling one.

## Phase 3 — data access to Polecat

`Data/ShipmentRepository.cs` is gone. So is Dapper, so is `PersistMessagesWithSqlServer`,
and so is every `await session.…` in a handler. `Shipment` is a Polecat document stored as
native SQL Server 2025 `json`, and the handlers declare what should be written rather than
writing it.

### The debt phase 1 left, paid

The repository opened its **own** connection, so a handler's database write and the outbox
rows for its cascading messages were two separate transactions. A crash between them lost
one or the other. Polecat's `IntegrateWithWolverine()` stands Wolverine's message store up
over the same SQL Server connection Polecat is using, so the document write, the saga state
and the outbox rows now commit together.

That is also why `PersistMessagesWithSqlServer(...)` disappeared rather than moving —
`IntegrateWithWolverine()` registers the message store itself.

### Declarative persistence, and where it stops

| Was | Is |
|---|---|
| `await repository.InsertAsync(shipment)` | return `Storage.Insert(shipment)` |
| `LoadAsync` + null check + throw | `[Entity(Required = true, OnMissing = OnMissing.ThrowException)]` |
| `await repository.UpdateStatusAsync(id, "Cancelled")` | mutate the loaded entity, return `Storage.Update(shipment)` |
| `Task<Shipment?> Get(Guid id, ShipmentRepository r)` | `Shipment Get([Entity] Shipment shipment)` |
| `Task<IReadOnlyList<Shipment>> GetAll(ShipmentRepository r)` | `[All] IReadOnlyList<Shipment> shipments` |

Three of the four message handlers are now **synchronous pure functions** — no session, no
repository, no `await`, nothing to mock. Returning an `IStorageAction<T>` turns transactional
middleware on by itself; no `[Transactional]` attribute is involved.

`[All]` deserves its caveat rather than its applause: it is a deliberately unfiltered
`select *`, right only while this table stays small. A real one wants a compiled query or a
query plan.

### The interesting part: a document store writes the whole document

The Dapper version wrote **columns** — `set LastLocation = @location` — so a scan handler and
a label handler touching different fields of the same shipment could not clobber each other.
That was never a design decision; it was a property of the SQL. A document store writes the
whole document, and it evaporates.

Two things put it back.

**`Shipment` implements `IRevisioned`.** Polecat detects the interface, keeps a numeric
revision, and stamps the expected revision into the UPDATE's `WHERE`. A losing write throws
`ConcurrencyException` instead of silently discarding the winner's change, and Program.cs
retries it with a short cooldown. You can watch the revision climb in the API responses —
booked at 1, two scans and a label take it to 4.

**The 45-second label handler was split in two.** `[Entity]` loads at the *start* of the
chain, so keeping the write in `GenerateLabelHandler` would have meant reading a shipment,
holding it across a 30–90 second carrier call, and writing it back against the stalest
revision in the system — every concurrent scan losing, and every retry re-running the carrier
call. So `GenerateLabelHandler` is now pure integration and cascades a new
`RecordTrackingNumber` command; `RecordTrackingNumberHandler` owns the write and its conflict
window is microseconds.

**This is the phase-3 lesson.** Declarative persistence is not just less code — it makes the
lifetime of a loaded entity explicit, and an entity held across slow I/O becomes obviously
wrong instead of invisibly wrong.

### Phase 1's ordering decision earns its keep

`CarrierScanHandler`'s idempotency guard used to be a SQL `WHERE` clause and is now an
ordinary `if`. That is only safe because a read-modify-write for one shipment is never in
flight twice at once — which is exactly what `MessagePartitioning.GlobalPartitioned` over
sharded queues bought in phase 1. Had the ordering been left to a per-listener
`PartitionProcessingByGroupId` call, this `if` would race across nodes.

Verified against the running service: a scan at 10:00 advances the document, a *later*
delivery of an 09:00 scan leaves it untouched at the same revision, and an 11:00 scan advances
it again.

### Two behaviour changes worth naming

- **A command for an unknown shipment now dead-letters.** `[Entity]`'s default in a message
  handler is `Simple404` — log it and stop — which would make it vanish quietly.
  `OnMissing.ThrowException` plus a `RequiredDataMissingException` policy keeps the
  NServiceBus behaviour. It also makes visible something the Dapper version hid: a scan for a
  shipment that does not exist used to update nothing and publish `ShipmentLocationUpdated`
  anyway.
- **A cancelled shipment is no longer resurrected by a late label.** `SetTrackingNumberAsync`
  set `Status = 'Labelled'` unconditionally. Easy to miss inside a SQL string; hard to miss in
  `if (shipment.Status == "Booked")`.

### Three things only running it could find

Phases 1 and 2 were correct-by-reading and never executed. Phase 3 started SQL Server 2025 and
RabbitMQ and ran the thing, and the first three failures had nothing to do with Polecat:

1. **`UriFormatException` at startup.** `appsettings.json` still held the NServiceBus-shaped
   `"host=localhost"`, interpolated into `$"amqp://{...}"` — `amqp://host=localhost`. Two clean
   compiles never saw it; the first `dotnet run` died on it.
2. **`AddWolverineHttp()` was missing.** Wolverine throws
   *"Required usage of IServiceCollection.AddWolverineHttp() is necessary"* from
   `MapWolverineEndpoints()` — at runtime, never at compile time.
3. **Wolverine 6 removed the Roslyn runtime compiler from core `WolverineFx`** (GH-2876). The
   host refuses to start without either the `WolverineFx.RuntimeCompilation` package or
   pre-generated code and `TypeLoadMode.Static`. This sample takes the package; production
   deployments generally pre-generate.

**A compile is not a smoke test.** Every one of these is a startup failure, and no amount of
reading found them.

### Not done here, deliberately

The shipment is a **document**, not an event stream. The prompt was to replace the SQL
persistence, and converting to event sourcing is a different exercise with different
trade-offs — the messages this service publishes are integration events, not a stream it
folds state from. Polecat would host that happily; it is just not what phase 3 asked for.

## Phase 4 — integration tests over every handler and endpoint

`Tests/` covers all five HTTP endpoints, all five message handlers and every saga
transition: **33 tests in about two seconds**, against a real SQL Server 2025, a real
RabbitMQ, real Wolverine code generation and the application's own `Program.cs`.
Nothing sleeps.

### Two substitutions, and only two

`AppFixture` overrides exactly two things, and neither is part of the system under test:

- **A database of its own** (`ShipmentTracking_Testing`), so running the suite cannot
  wipe whatever you were looking at.
- **`ICarrierLabelClient`**, because `FakeCarrierLabelClient` sleeps 45 seconds on
  purpose and a test may not. That duration is the whole reason `label-generation` is a
  Durable listener; it belongs in the running application, not in a test.

Everything else is the real thing — including the Polecat store, the outbox, the three
RabbitMQ listeners and the sharded `carrier-events` topology.

### ⚠️ The mistake this suite was nearly built on

The usual advice for a Wolverine test host is
`services.DisableAllExternalWolverineTransports()`. **For this application it produces a
suite in which nothing happens and everything passes.**

Stubbing sets `ExternalTransportsAreStubbed`, which replaces each external sender with a
`NullSender`. It does not reroute anything locally. Every command here is routed
`ToRabbitQueue(...)` by `Program.cs`, so the first exploratory test printed exactly this:

```
Sent: BookShipment -> rabbitmq://queue/shipment-commands
SHIPMENTS IN DB: 0
```

A green test over a system that had done nothing. Stubbing is right for an application
whose messages route locally by convention. It is wrong for one whose topology is the
point.

So the suite talks to the real broker and turns on `IncludeExternalTransports()`, without
which a `Sent` record to a non-`local://` destination is marked complete the instant the
send is made — same vacuous result by a different route. One HTTP call now traces
end to end in about a second:

```
Sent/Received/Executed  BookShipment          -> rabbitmq://queue/shipment-commands
Sent/Received/Executed  ShipmentBooked        -> local://…/       (starts the saga)
Sent (scheduled)        DeliverySlaExpired    -> local://…/       (five days out)
Sent/Received/Executed  GenerateLabel         -> rabbitmq://queue/label-generation
Sent/Received/Executed  RecordTrackingNumber  -> rabbitmq://queue/shipment-commands
Sent/Received/Executed  LabelGenerated        -> local://…/
```

### Nothing sleeps, including the five-day timeout

Every wait is a wait for *work*, never for a duration:

| What is being waited for | How |
|---|---|
| An HTTP call and everything it cascades | `Host.Scenario` inside `TrackActivity().ExecuteAndWaitAsync` |
| A message handler and its cascade | `TrackActivity().InvokeMessageAndWaitAsync` |
| **The saga's five-day SLA timeout** | `PlayScheduledMessagesAsync(30.Seconds())` |

`PlayScheduledMessagesAsync` replays the captured scheduled envelopes immediately and
hands back a fresh tracked session. A sleep could not have tested this at all.

The `Timeout(30.Seconds())` on every session is a **ceiling, not a duration** — it costs
nothing when the work finishes in 40ms, which it does.

### Proving the suite can fail

A tracked-session suite that has never been seen fail has not been tested — the failure
mode is a *green* test that raced its own assertions. Every handler and the saga were
mutated one at a time and the suite was re-run:

| Mutation | Tests red |
|---|---|
| Remove the stale-scan guard | 1 |
| Stop writing `Status = "Delivered"` | 2 |
| Stop writing `Status = "Cancelled"` | 2 |
| Let a late label resurrect a cancelled shipment | 1 |
| Saga stops scheduling the SLA timeout | 4 |
| Escalate even when delivered | 1 |
| Saga stops cascading `GenerateLabel` | 5 |

### Three things the tests found

**1. A business rule that could never fire.** `CancelShipmentHandler` refuses to cancel a
shipment whose status is `"Delivered"` — and *nothing in the application ever wrote that
status.* Not the NServiceBus original, not the Dapper port; `RecordScanAsync` touched only
the location columns. The rule was written, reviewed and carried across three phases
without ever being reachable. Writing the test for it is what surfaced it.

**2. A race that only the carrier's slowness was hiding.** `BookShipmentHandler` cascaded
`ShipmentBooked` (which starts the saga) and `GenerateLabel` in parallel. With a real
carrier taking 30–90 seconds, `ShipmentBooked` always won. With an instant test double it
did not, and `LabelGenerated` reached the saga before the saga existed —
`UnknownSagaException`, on a cold database, in one test out of 29.

The fix is structural rather than a retry: `GenerateLabel` is now cascaded from the saga's
`Start`. Wolverine commits the saga insert and that method's outgoing messages in one
transaction, so `GenerateLabel` cannot leave before the saga row exists.

> **This is the phase-4 lesson.** "It works because that call is slow" is not a
> correctness argument — it is the same reasoning phase 1 rejected when it refused to put
> `label-generation` on `NativeAck`. The difference is that phase 1 caught it by thinking
> and phase 4 caught it by running.

**3. An event that goes nowhere.** `ShipmentLocationUpdated` is published on every scan,
and this application declares no subscriber and no route for it — Wolverine records
`NoRoutes` and drops it. Carried over from the NServiceBus original, where a subscriber
elsewhere picked it up. The tests assert the `NoRoutes` record rather than hiding it, so
if a route is ever added the assertion breaks and someone has to look at it. **It is a
finding, not a design.**

### Reading the generated code instead of guessing

When a declarative attribute does not resolve, preview what Wolverine actually generated
rather than reasoning about it:

```bash
dotnet run --project ShipmentTracking -- wolverine-diagnostics codegen-preview --route "GET /shipments"
dotnet run --project ShipmentTracking -- wolverine-diagnostics codegen-preview --handler CancelShipment
dotnet run --project ShipmentTracking -- wolverine-diagnostics describe-routing --all
dotnet run --project ShipmentTracking -- codegen test    # compile every chain, no host start
```

The first one prints the `[All]` endpoint's body — `documentSession.Query<Shipment>()`
piped through `ToListAsync`, bound to the *outboxed* Polecat session.

## Phase 5 — Aspire and CritterWatch

Two new projects, and the `docker-compose.yml` demoted from the primary way to run this
to the fallback for people who would rather not run Aspire.

```
ShipmentTracking/
├── AppHost/            .NET Aspire — provisions SQL Server 2025 + RabbitMQ, orders start-up
├── CritterWatchHost/   the monitoring console (its own database, deliberately)
├── Tests/              33 integration tests
└── …                   the service itself
```

```bash
dotnet run --project ShipmentTracking/AppHost
```

That is the whole setup step now. Aspire pulls both containers, creates both databases,
generates the credentials, injects the connection strings, starts the console, waits for
it, then starts the service.

### The console does not share the service's database

`AppHost` puts two databases on one SQL Server:

```csharp
var shipmentsDb  = sql.AddDatabase("shipments",     "ShipmentTracking");
var critterStore = sql.AddDatabase("critterstore",  "CritterWatch");
```

That separation is the point, not tidiness. **A monitoring console that dies alongside
the thing it monitors is not a monitoring console** — and CritterWatch's metrics table is
the fastest-growing table in either system, which is not something to put next to your
shipments.

`critterstore` is also a worked example of an Aspire rule that bites: **resource names are
unique case-insensitively across resource types.** The console *project* is called
`critterwatch`, so its database cannot be. The second `AddDatabase` argument keeps the real
database named `CritterWatch` so the non-Aspire fallback connection string still points
somewhere sensible.

### Three things that are not optional

**`.WithImageTag("2025-latest")` on SQL Server.** Aspire's default tag is not 2025, and
Polecat requires v17+ for the native `json` column type. On an older image it fails at
schema creation with a message that never mentions the version.

**`.WithHttpEndpoint(env: "ASPNETCORE_HTTP_PORTS")` on both projects.** Neither ships a
`launchSettings.json`, so Aspire has no launch profile to read an endpoint from. Without
this the console never starts at all and the service binds port 5000 outside Aspire's
knowledge — the first run of this AppHost did exactly that, and the dashboard showed a
service with no endpoint next to a console that had never launched.

**`.WaitFor(...)` on every reference.** Wolverine's `AutoProvision()` declares exchanges and
queues, and Polecat creates its tables, the moment the host boots. `WaitFor` gates start-up
on the container's health check; without it, provisioning races the container and fails
intermittently. Monitored services additionally `.WaitFor(critterwatch)` so the shared
`critterwatch` queue exists before the first registration message is published.

### The console needs its schema, and nothing was going to create it

The console starts, listens, serves the SPA — and then fails **every** inbound telemetry
message:

```
Microsoft.Data.SqlClient.SqlException: Invalid object name 'critterwatch.pc_streams'.
```

Polecat creates **document** tables on demand, but the event tables (`pc_streams`,
`pc_events`) come from the resource model, and under Aspire the console is simply started —
it never gets a CLI invocation to provision anything. One line fixes it:

```csharp
builder.Services.AddResourceSetupOnStartup();
```

The failure is nasty because the console looks healthy from the outside. HTTP 200, SPA
served, dashboard loads, and not one message ever lands.

### The DLQ argument that has to match on both sides

Neither `Program.cs` calls `.DisableDeadLetterQueueing()`, and that is a decision rather
than an omission.

The console and the service share one broker, and **both** declare the well-known
`critterwatch` queue. RabbitMQ rejects an inequivalent redeclare of an existing queue with
`PRECONDITION_FAILED` (406), so the queue's dead-letter arguments must be *identical* on
every side. Disabling DLQ on the console while the service leaves it at the Wolverine
default means whichever process starts second dies at startup. Leaving both at the default
is the simplest way to keep them the same.

### Monitoring is off unless something is listening

```csharp
if (builder.Configuration.GetValue("CritterWatch:Enabled", false))
{
    opts.AddCritterWatchMonitoring(
        critterWatchUri:  new Uri("rabbitmq://queue/critterwatch"),
        systemControlUri: new Uri("rabbitmq://queue/shipmenttracking_control"));
}
```

Default `false` in `appsettings.json`; the AppHost sets `CritterWatch__Enabled=true`, because
Aspire is the environment where a console exists. A plain `dotnet run` under
`docker-compose` publishes no telemetry to a queue nobody reads.

`critterWatchUri` is shared by every monitored service — the console listens there.
`systemControlUri` is **unique per service**: it is how the console sends commands *back*,
to pause a listener, drain a queue or replay a dead letter.

### And it is off in the tests

```csharp
services.DisableCritterWatch();
```

`AddCritterWatchMonitoring` also enables message-causation and event-append tracking, which
do per-envelope work in the hot path, and no test consumes the telemetry. `DisableCritterWatch()`
is order-independent — it registers nothing if called first, and removes exactly what was
registered if called after.

> Do **not** do this by branching on `IHostEnvironment`. It fails in both directions: Alba
> runs as `Development` by default so the branch would not fire where it is needed, and a
> developer running this service against a real local console **is** a normal `Development`
> activity, so disabling there would break the setup they most want working.

### Version coupling — resolve it, do not copy it

CritterWatch pins the Wolverine line it was compiled against. The published guidance quotes
a snapshot; **the snapshot was wrong**, and checking took one command:

```bash
dotnet list package --include-transitive | grep -i wolverine
```

`CritterWatch.SqlServer` 1.0.1 actually resolves WolverineFx **6.29.1**, JasperFx 2.52.1 and
Polecat 5.19.0 — not the 6.30.0 / 2.55.0 / 5.19.2 the docs quoted. The console is therefore
pinned to 6.29.1 and stays internally consistent; ShipmentTracking stays on 6.30.0. They are
separate processes, so what has to line up is *within* each host, and the wire format between
them is version-tolerant brotli JSON.

### Verified, not assumed

The fleet was run and the console's own database inspected:

```
service           | type           | version
ShipmentTracking  | ServiceSummary | 39

total_events   39
metric_samples  5
```

Thirty-nine events on a `ServiceSummary` stream keyed by service name — capabilities,
endpoint health, broker health, leadership, node lifecycle, message causation — plus real
metrics samples, after booking a shipment and posting a carrier scan through the
Aspire-assigned ports.

## Code review — what a second pass found

The five phases were reviewed after the fact, adversarially. Three things came out of it,
and the first two are the interesting ones because **the phase 4 test suite had already
covered the neighbouring case and still missed them.**

### 1. Three saga messages that dead-lettered when the saga was already gone

Wolverine throws `UnknownSagaException` for a saga message whose saga cannot be loaded,
unless the saga declares a `NotFound` method for that message type. `ShipmentDeliverySaga`
declared none, and three of its four messages are reachable after it has completed itself:

| Message | How it happens |
|---|---|
| `LabelGenerated` | Cancel a shipment while the 30–90 second carrier call is in flight |
| `ShipmentDelivered` | A carrier sends a second `DELIVERED` scan with a newer timestamp |
| `ShipmentCancelled` | Cancel twice — the handler only refuses a *delivered* shipment |

Each one is now a `NotFound` method, and each has a test that was verified to go red without
it. `NotFound` may be `static` — only `Start` and `NotFound` may be, since both assume the
saga does not exist yet.

**Why phase 4 missed it:** `a_late_label_does_not_resurrect_a_cancelled_shipment` tested
exactly this race — and asserted on the *document*, by invoking `RecordTrackingNumber`
directly. The saga-bound `LabelGenerated` was never delivered, so the half of the race that
throws was never exercised. Testing a scenario is not the same as testing every consumer in it.

### 2. `DeliverySlaExpired` needs no `NotFound`, and that took the generated code to settle

The five-day SLA timeout lands on a completed saga for **every** delivered or cancelled
shipment — the most common not-found case in the system. It does not throw, and the
documentation's example of when you need `NotFound` is precisely this case.

`codegen-preview` settles it in one command:

```bash
dotnet run --project ShipmentTracking -- wolverine-diagnostics codegen-preview --handler DeliverySlaExpired
dotnet run --project ShipmentTracking -- wolverine-diagnostics codegen-preview --handler LabelGenerated
```

```csharp
// DeliverySlaExpired : TimeoutMessage
if (shipmentDeliverySaga_sagaId == null) { return; }

// LabelGenerated — a plain record
if (shipmentDeliverySaga_sagaId == null)
{
    throw new Wolverine.Persistence.Sagas.UnknownSagaException(typeof(ShipmentDeliverySaga), sagaId);
}
```

**Wolverine special-cases `TimeoutMessage`** — `SagaChain` checks
`MessageType.CanBeCastTo<TimeoutMessage>()` and emits a silent `return` instead of the throw.
So a timeout subclassing `TimeoutMessage` is already safe; a plain message you scheduled
yourself as a timeout is not. That distinction is not in the prose anywhere, and reading the
generated code is how you get it.

### 3. A license key lookup that could never succeed

The AppHost read `builder.Configuration["JASPERFX__LICENSEKEY"]`. **That is always `null`.**
.NET's environment-variable configuration provider translates `__` into `:`, so the variable
`JASPERFX__LICENSEKEY` arrives as the configuration *key* `JasperFx:LicenseKey` — which is
what CritterWatch itself reads.

The failure is silent and it inverts the block's own purpose: the propagation exists so that
license-gated operator actions work on the monitored services, and instead it never ran.

### Deliberate, not oversights

- **`version` appears in every API response.** That is Polecat's revision, and it is left
  visible on purpose — the README uses it as evidence above, and a client doing conditional
  updates would want it.
- **Credentials are hard-coded** in `appsettings.json`, `docker-compose.yml` and the test
  fixture. Fine for a sample with a throwaway container; under Aspire they are generated and
  injected instead, which is the better story and the one to copy.
- **The test suite targets `localhost,1433`**, so it needs `docker compose up -d` rather than
  the AppHost — Aspire assigns random host ports by design.

## Running it

**Preferred — Aspire.** One command; no manual database creation, no connection strings:

```bash
dotnet run --project ShipmentTracking/AppHost
```

The containers are declared `ContainerLifetime.Persistent`, so they survive an AppHost
restart and your data with them. `docker rm -f` them when you want a clean slate.

**Fallback — docker-compose.** `docker-compose.yml` in this directory brings up SQL Server
2025 and RabbitMQ. Polecat needs SQL Server **2025** (v17+) for the native `json` column
type; earlier images fail at schema creation.

```bash
docker compose up -d

# Polecat creates its tables on demand, but not the database itself
docker exec shipmenttracking-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P 'P@ssw0rd!' -C \
    -Q "if db_id('ShipmentTracking') is null create database ShipmentTracking"

dotnet run --project ShipmentTracking -- resources setup   # schema, envelope tables, Rabbit topology
dotnet run --project ShipmentTracking
```

> `resources setup`, **not** `db-apply`. Polecat exposes its schema through JasperFx's
> `ISystemPart` / `IStatefulResource`, not as a Weasel `IDatabaseSource` the way Marten does,
> so `db-apply` reports *"No Weasel databases were registered in this application."*

`dotnet run --project ShipmentTracking -- codegen test` compiles every generated handler and
endpoint without starting the host — the fastest way to check that `[Entity]`, `[All]` and the
saga all resolve.

### The tests

```bash
docker compose up -d
dotnet test ShipmentTracking/Tests/Tests.csproj
```

The fixture creates `ShipmentTracking_Testing` itself, so no setup step is needed beyond
the containers. The suite needs the RabbitMQ from `docker-compose.yml` — it talks to the
real broker on purpose; see the phase 4 notes above.
