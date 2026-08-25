# ShipmentTracking

A Wolverine service converted from NServiceBus. Phases 1–3 of a migration walkthrough:
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

## Running it

`docker-compose.yml` in this directory brings up SQL Server 2025 and RabbitMQ. Polecat needs
SQL Server **2025** (v17+) for the native `json` column type; earlier images fail at schema
creation.

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
